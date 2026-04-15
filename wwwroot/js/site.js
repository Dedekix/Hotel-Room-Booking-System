// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// ── Custom Confirmation Modal ──────────────────────────────────────────────
(function () {
    function ensureModal() {
        if (document.getElementById('gh-confirm-overlay')) return;
        const div = document.createElement('div');
        div.innerHTML = `
        <div id="gh-confirm-overlay" style="display:none;position:fixed;inset:0;background:rgba(0,0,0,0.45);align-items:center;justify-content:center;z-index:9999;">
            <div id="gh-confirm-box">
                <div id="gh-confirm-icon"><i class="fa-solid fa-triangle-exclamation"></i></div>
                <p id="gh-confirm-msg"></p>
                <div id="gh-confirm-btns">
                    <button id="gh-confirm-cancel">Cancel</button>
                    <button id="gh-confirm-ok">Confirm</button>
                </div>
            </div>
        </div>`;
        document.body.appendChild(div.firstElementChild);
        document.getElementById('gh-confirm-cancel').addEventListener('click', closeModal);
        document.getElementById('gh-confirm-overlay').addEventListener('click', function (e) {
            if (e.target === this) closeModal();
        });
    }

    function closeModal() {
        const el = document.getElementById('gh-confirm-overlay');
        if (el) el.style.display = 'none';
    }

    window.ghConfirm = function (e, message, onConfirm) {
        e.preventDefault();
        e.stopPropagation();
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', function () { showModal(message, onConfirm); });
        } else {
            showModal(message, onConfirm);
        }
    };

    function showModal(message, onConfirm) {
        ensureModal();
        document.getElementById('gh-confirm-msg').textContent = message;
        const overlay = document.getElementById('gh-confirm-overlay');
        overlay.style.display = 'flex';
        const okBtn = document.getElementById('gh-confirm-ok');
        const newOk = okBtn.cloneNode(true);
        okBtn.parentNode.replaceChild(newOk, okBtn);
        newOk.addEventListener('click', function () {
            closeModal();
            onConfirm();
        });
    }
})();
