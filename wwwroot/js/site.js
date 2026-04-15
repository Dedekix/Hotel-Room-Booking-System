// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// ── Custom Confirmation Modal ──────────────────────────────────────────────
(function () {
    const html = `
    <div id="gh-confirm-overlay" style="display:none">
        <div id="gh-confirm-box">
            <div id="gh-confirm-icon"><i class="fa-solid fa-triangle-exclamation"></i></div>
            <p id="gh-confirm-msg"></p>
            <div id="gh-confirm-btns">
                <button id="gh-confirm-cancel">Cancel</button>
                <button id="gh-confirm-ok">Confirm</button>
            </div>
        </div>
    </div>`;
    document.addEventListener('DOMContentLoaded', function () {
        document.body.insertAdjacentHTML('beforeend', html);
        document.getElementById('gh-confirm-cancel').addEventListener('click', function () {
            document.getElementById('gh-confirm-overlay').style.display = 'none';
        });
        document.getElementById('gh-confirm-overlay').addEventListener('click', function (e) {
            if (e.target === this) this.style.display = 'none';
        });
    });

    window.ghConfirm = function (e, message, onConfirm) {
        e.preventDefault();
        document.getElementById('gh-confirm-msg').textContent = message;
        const overlay = document.getElementById('gh-confirm-overlay');
        overlay.style.display = 'flex';
        const okBtn = document.getElementById('gh-confirm-ok');
        const newOk = okBtn.cloneNode(true);
        okBtn.parentNode.replaceChild(newOk, okBtn);
        newOk.addEventListener('click', function () {
            overlay.style.display = 'none';
            onConfirm();
        });
    };
})();
