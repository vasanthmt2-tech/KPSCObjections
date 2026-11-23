/*
 * Encrypts every form submission of the page before it leaves the browser
 * (see Security/EncryptedForms.cs on the server).
 *
 * For each submit:
 *   - all form fields (and any chosen files) are collected;
 *   - a fresh random AES-256 key and HMAC-SHA256 key are made;
 *   - the fields are encrypted with AES-CBC and sealed with the HMAC;
 *   - the two keys are encrypted with the server's RSA public key (RSA-OAEP-256),
 *     read from <meta name="kpsc-enc-key">;
 *   - one value, __ENC=key.iv.data.mac, is posted as text/plain.
 * Field names and values never appear in the request.
 *
 * It catches both ways ASP.NET Web Forms submits: buttons (submit event, with
 * the clicked button's name and value) and __doPostBack (form.submit()), used
 * by AutoPostBack dropdowns and link buttons. If the browser cannot encrypt
 * (Web Crypto needs HTTPS), nothing is sent.
 */
(function () {
    'use strict';

    var meta = document.querySelector('meta[name="kpsc-enc-key"]');

    if (!meta || !meta.getAttribute('content')) {
        return;
    }

    var keyParts = meta.getAttribute('content').split('.');
    var nativeSubmit = HTMLFormElement.prototype.submit;
    var plainMarker = 'data-kpsc-encrypted';
    var busy = false;
    var lastSubmitter = null;
    var publicKeyPromise = null;

    // The server refuses (HTTP 400) any field where "<" is followed by a
    // letter, "!", "/" or "?", or that contains "&#" (ASP.NET request
    // validation), and the page with everything typed is lost. Such text is
    // caught here first, so the form stays as it is.
    var refusedText = /<[a-zA-Z!\/?]|&#/;

    function fieldName(field) {
        var label = field.id ? document.querySelector('label[for="' + field.id + '"]') : null;
        var text = label ? label.textContent : (field.getAttribute('placeholder') || field.name || '');
        return text.replace(/\*/g, '').replace(/\s+/g, ' ').trim();
    }

    function refusedField(form) {
        var fields = form.querySelectorAll('textarea, input[type="text"], input[type="password"], input[type="search"], input:not([type])');

        for (var i = 0; i < fields.length; i++) {
            if (!fields[i].disabled && refusedText.test(fields[i].value || '')) {
                return fields[i];
            }
        }

        return null;
    }

    function toBase64(buffer) {
        var bytes = new Uint8Array(buffer);
        var text = '';

        for (var i = 0; i < bytes.length; i += 0x8000) {
            text += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
        }

        return btoa(text);
    }

    function publicKey() {
        if (!publicKeyPromise) {
            publicKeyPromise = window.crypto.subtle.importKey(
                'jwk',
                { kty: 'RSA', n: keyParts[0], e: keyParts[1], alg: 'RSA-OAEP-256', ext: true },
                { name: 'RSA-OAEP', hash: 'SHA-256' },
                false,
                ['encrypt']);
        }

        return publicKeyPromise;
    }

    function collect(form, submitter) {
        var data = new FormData(form);

        if (submitter && submitter.name && form.contains(submitter)) {
            data.append(submitter.name, submitter.value || '');
        }

        var fields = [];
        var files = [];

        data.forEach(function (value, name) {
            if (typeof value === 'string') {
                fields.push([name, value]);
            } else {
                files.push([name, value]);
            }
        });

        return Promise.all(files.map(function (entry) {
            var file = entry[1];

            return (file.size > 0 ? file.arrayBuffer() : Promise.resolve(new ArrayBuffer(0)))
                .then(function (buffer) {
                    return [entry[0], file.name || '', file.type || '', toBase64(buffer)];
                });
        })).then(function (uploads) {
            return JSON.stringify({ f: fields, u: uploads });
        });
    }

    function encrypt(json) {
        var subtle = window.crypto.subtle;
        var keys = window.crypto.getRandomValues(new Uint8Array(64));
        var iv = window.crypto.getRandomValues(new Uint8Array(16));
        var plain = new TextEncoder().encode(json);

        return Promise.all([
            subtle.importKey('raw', keys.slice(0, 32), { name: 'AES-CBC' }, false, ['encrypt']),
            subtle.importKey('raw', keys.slice(32), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']),
            publicKey()
        ]).then(function (k) {
            return subtle.encrypt({ name: 'AES-CBC', iv: iv }, k[0], plain).then(function (cipher) {
                var sealed = new Uint8Array(iv.length + cipher.byteLength);
                sealed.set(iv, 0);
                sealed.set(new Uint8Array(cipher), iv.length);

                return Promise.all([
                    subtle.sign('HMAC', k[1], sealed),
                    subtle.encrypt({ name: 'RSA-OAEP' }, k[2], keys)
                ]).then(function (r) {
                    return toBase64(r[1]) + '.' + toBase64(iv) + '.' + toBase64(cipher) + '.' + toBase64(r[0]);
                });
            });
        });
    }

    function send(form, submitter) {
        if (busy) {
            return;
        }

        if (!window.crypto || !window.crypto.subtle || !window.TextEncoder) {
            window.alert('This page could not send the form securely. Please open the portal over https:// and try again.');
            return;
        }

        var refused = refusedField(form);

        if (refused) {
            var name = fieldName(refused);
            window.alert((name ? name + ': ' : '') +
                'please remove "<" directly before a letter (for example write "x < y" with spaces, ' +
                'or "less than") and "&#", then try again. Nothing has been sent.');
            refused.focus();
            return;
        }

        busy = true;

        collect(form, submitter).then(encrypt).then(function (value) {
            var post = document.createElement('form');
            post.method = 'post';
            post.action = form.action;
            post.enctype = 'text/plain';
            post.acceptCharset = 'UTF-8';
            post.setAttribute(plainMarker, '');
            post.style.display = 'none';

            if (form.target) {
                post.target = form.target;
            }

            var input = document.createElement('input');
            input.type = 'hidden';
            input.name = '__ENC';
            input.value = value;
            post.appendChild(input);

            document.body.appendChild(post);
            nativeSubmit.call(post);

            // A file download (Excel, ZIP, PDF) answers the post without
            // leaving this page, so the page must be able to post again.
            // The fields were already copied, so clear the postback target
            // as a fresh page would have it; otherwise a later button click
            // could be taken as the earlier link's click.
            clearPostBackTarget(form);

            window.setTimeout(function () {
                busy = false;

                if (post.parentNode) {
                    post.parentNode.removeChild(post);
                }
            }, 1500);
        }).catch(function () {
            busy = false;
            window.alert('This page could not send the form securely. Please reload the page and try again.');
        });
    }

    function clearPostBackTarget(form) {
        ['__EVENTTARGET', '__EVENTARGUMENT'].forEach(function (name) {
            var field = form.elements[name];

            if (field && field.value !== undefined) {
                field.value = '';
            }
        });
    }

    // Coming back to this page from the browser's history: allow posting again.
    window.addEventListener('pageshow', function () {
        busy = false;
    });

    // Remember which submit button was used (for browsers without event.submitter).
    document.addEventListener('click', function (e) {
        var button = e.target && e.target.closest
            ? e.target.closest('input[type="submit"], button[type="submit"], button:not([type])')
            : null;

        if (button) {
            lastSubmitter = button;
        }
    }, true);

    // Buttons: runs after the form's own onsubmit (which may cancel it).
    document.addEventListener('submit', function (e) {
        var form = e.target;

        if (e.defaultPrevented || form.hasAttribute(plainMarker)) {
            return;
        }

        e.preventDefault();
        send(form, e.submitter || lastSubmitter);
        lastSubmitter = null;
    });

    // __doPostBack and any other script calling form.submit().
    HTMLFormElement.prototype.submit = function () {
        if (this.hasAttribute(plainMarker)) {
            return nativeSubmit.call(this);
        }

        send(this, null);
    };
})();
