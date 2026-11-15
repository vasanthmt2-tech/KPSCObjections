/*
 * Login page: after an OTP is requested, Resend OTP stays disabled for 60
 * seconds and the remaining time is shown in the message label. The server
 * puts the seconds left in data-resend-seconds on the label and renders the
 * button disabled; it also refuses a resend that comes too early, so this
 * script only makes the wait visible.
 */
(function () {
    'use strict';

    function start() {
        var label = document.querySelector('[data-resend-seconds]');
        var button = document.querySelector('[id$="btnResendOTP"]');

        if (!label || !button) {
            return;
        }

        var left = parseInt(label.getAttribute('data-resend-seconds'), 10);

        if (!(left > 0)) {
            return;
        }

        var countdown = document.createElement('span');
        label.appendChild(document.createTextNode(' '));
        label.appendChild(countdown);

        var timer;

        function tick() {
            if (left > 0) {
                button.disabled = true;
                countdown.textContent = 'You can request a new OTP in ' + left +
                    ' second' + (left === 1 ? '' : 's') + '.';
                left -= 1;
            } else {
                button.disabled = false;
                countdown.textContent = 'If the OTP has not arrived, check the details and click Resend OTP.';
                window.clearInterval(timer);
            }
        }

        tick();
        timer = window.setInterval(tick, 1000);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', start);
    } else {
        start();
    }
})();
