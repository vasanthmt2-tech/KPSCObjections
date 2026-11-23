/*
 * Date of birth box on the candidate login page (data-dob-mask): as the
 * candidate types digits, the dashes of DD-MM-YYYY are added after the day
 * and the month. Anything that is not a digit is dropped, so a pasted
 * "15/03/1998" becomes "15-03-1998". Deleting works normally: a dash is
 * added only while typing forwards, so Backspace can remove it.
 * The server still checks the date (Login.aspx.cs).
 */
(function () {
    'use strict';

    function format(digits, addTrailingDash) {
        var d = digits.slice(0, 8);
        var text = d.slice(0, 2);

        if (d.length > 2 || (addTrailingDash && d.length === 2))
            text += '-' + d.slice(2, 4);

        if (d.length > 4 || (addTrailingDash && d.length === 4))
            text += '-' + d.slice(4, 8);

        return text;
    }

    function attach(box) {
        box.addEventListener('input', function (e) {
            var deleting = e.inputType && e.inputType.indexOf('delete') === 0;
            var atEnd = box.selectionStart === box.value.length;
            var formatted = format(box.value.replace(/\D/g, ''), !deleting);

            if (formatted !== box.value) {
                box.value = formatted;

                // Keep the caret at the end when the candidate was typing at the end.
                if (atEnd)
                    box.setSelectionRange(formatted.length, formatted.length);
            }
        });
    }

    function init() {
        var boxes = document.querySelectorAll('input[data-dob-mask]');

        for (var i = 0; i < boxes.length; i++)
            attach(boxes[i]);
    }

    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', init);
    else
        init();
})();
