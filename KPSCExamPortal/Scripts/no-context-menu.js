/*
 * Disables the right-click (context) menu on the portal's pages.
 *
 * Right-click still works inside text boxes and other editable fields, so
 * candidates can paste a registration number or OTP.
 *
 * Note: this only removes the menu. The page source remains available
 * through the browser's own menus and developer tools; nothing secret is
 * ever sent to the browser, so it is not a security control.
 */
(function () {
    'use strict';

    var editable = 'input, textarea, select, [contenteditable=""], [contenteditable="true"]';

    document.addEventListener('contextmenu', function (e) {
        var target = e.target;

        if (target && target.closest && target.closest(editable)) {
            return;
        }

        e.preventDefault();
    });
})();
