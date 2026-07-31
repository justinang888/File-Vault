(function () {
    "use strict";

    const dropzone = document.getElementById("dropzone");
    const input = document.getElementById("file");
    const nameWrap = document.getElementById("fileName");
    const nameText = document.getElementById("fileNameText");
    const uploadBtn = document.getElementById("uploadBtn");

    function reflectSelection() {
        if (!input) return;
        const hasFile = input.files && input.files.length > 0;
        if (dropzone) dropzone.classList.toggle("has-file", hasFile);
        if (uploadBtn) uploadBtn.disabled = !hasFile;
        if (hasFile && nameText) nameText.textContent = input.files[0].name;
    }

    if (dropzone && input) {
        input.addEventListener("change", reflectSelection);

        ["dragenter", "dragover"].forEach(function (evt) {
            dropzone.addEventListener(evt, function (e) {
                e.preventDefault();
                dropzone.classList.add("is-dragover");
            });
        });

        ["dragleave", "dragend", "drop"].forEach(function (evt) {
            dropzone.addEventListener(evt, function (e) {
                e.preventDefault();
                dropzone.classList.remove("is-dragover");
            });
        });

        dropzone.addEventListener("drop", function (e) {
            if (e.dataTransfer && e.dataTransfer.files.length) {
                input.files = e.dataTransfer.files;
                reflectSelection();
            }
        });
    }

    // Copy-to-clipboard for share links.
    document.querySelectorAll(".share-copy").forEach(function (btn) {
        btn.addEventListener("click", function () {
            const value = btn.getAttribute("data-copy");
            if (!value) return;

            const done = function () {
                const original = btn.textContent;
                btn.textContent = "Copied";
                btn.classList.add("is-copied");
                setTimeout(function () {
                    btn.textContent = original;
                    btn.classList.remove("is-copied");
                }, 1600);
            };

            if (navigator.clipboard && navigator.clipboard.writeText) {
                navigator.clipboard.writeText(value).then(done).catch(function () {
                    fallbackCopy(value, done);
                });
            } else {
                fallbackCopy(value, done);
            }
        });
    });

    function fallbackCopy(text, onDone) {
        const temp = document.createElement("textarea");
        temp.value = text;
        temp.style.position = "fixed";
        temp.style.opacity = "0";
        document.body.appendChild(temp);
        temp.select();
        try { document.execCommand("copy"); } catch (e) { /* no-op */ }
        document.body.removeChild(temp);
        onDone();
    }

    // Auto-dismiss flash messages.
    document.querySelectorAll(".flash").forEach(function (flash) {
        setTimeout(function () {
            flash.style.transition = "opacity 0.4s ease, transform 0.4s ease";
            flash.style.opacity = "0";
            flash.style.transform = "translateY(-6px)";
            setTimeout(function () { flash.remove(); }, 400);
        }, 4000);
    });
})();
