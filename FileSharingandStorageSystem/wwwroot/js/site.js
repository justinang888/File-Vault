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
