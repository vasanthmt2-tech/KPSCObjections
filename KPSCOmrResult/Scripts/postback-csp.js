/*
 * ASP.NET Web Forms writes AutoPostBack controls (dropdowns, checkboxes) as
 *   onchange="setTimeout('__doPostBack(\'name\',\'arg\')', 0)"
 * i.e. it passes code to setTimeout as a string. The site's
 * Content-Security-Policy does not allow 'unsafe-eval', so the browser refuses
 * to run strings as code and the postback never happens.
 *
 * This wraps setTimeout: a string that is exactly a __doPostBack call is run
 * by calling __doPostBack directly. Any other string is still refused, so no
 * text is ever turned into code.
 */
(function () {
    'use strict';

    var original = window.setTimeout;
    var postBack = /^\s*__doPostBack\('((?:[^'\\]|\\.)*)'\s*,\s*'((?:[^'\\]|\\.)*)'\)\s*;?\s*$/;

    function unescape(value) {
        return value.replace(/\\(.)/g, '$1');
    }

    window.setTimeout = function (handler, delay) {
        if (typeof handler === 'string') {
            var match = postBack.exec(handler);

            if (!match) {
                return original.apply(window, arguments);
            }

            var target = unescape(match[1]);
            var argument = unescape(match[2]);

            handler = function () {
                window.__doPostBack(target, argument);
            };
        }

        var args = Array.prototype.slice.call(arguments);
        args[0] = handler;
        return original.apply(window, args);
    };
})();
