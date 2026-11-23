// Theme

(function () {
    try {
        var saved = localStorage.getItem("theme");
        if (saved === "dark" || saved === "light") {
            document.documentElement.setAttribute("data-bs-theme", saved);
        }
    } catch (e) { }
})();

(function () {
    const root = document.documentElement;
    const btn = document.getElementById("themeToggle");
    const icon = document.getElementById("themeIcon");

    function currentTheme() {
        return root.getAttribute("data-bs-theme") ||
            (window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
    }

    function render() {
        icon.textContent = currentTheme() === "dark" ? "☀️" : "🌙";
    }

    btn.addEventListener("click", function () {
        const next = currentTheme() === "dark" ? "light" : "dark";
        root.setAttribute("data-bs-theme", next);
        try { localStorage.setItem("theme", next); } catch (e) { }
        render();
    });

    render();
})();

// Theme

// Confirm
// data-confirm="..." taşıyan submit butonları, formu göndermeden önce onay ister.
// Örnek: <button type="submit" data-confirm="Silmek istediğinize emin misiniz?">Sil</button>

(function () {
    document.addEventListener("click", function (e) {
        const btn = e.target.closest("[data-confirm]");
        if (!btn || !btn.form) return;

        e.preventDefault();

        const message = btn.dataset.confirm;

        // requestSubmit(btn): butonun formaction/name değerleri korunur, click tekrar tetiklenmez
        const submit = function () { btn.form.requestSubmit(btn); };

        // SweetAlert yüklenemediyse (ör. CDN'e erişim yok) tarayıcının onay penceresi kullanılır
        if (typeof Swal === "undefined") {
            if (window.confirm(message)) submit();
            return;
        }

        Swal.fire({
            icon: "warning",
            title: "Emin misiniz?",
            text: message,
            showCancelButton: true,
            confirmButtonText: "Evet",
            cancelButtonText: "Vazgeç",
            focusCancel: true
        }).then(function (result) {
            if (result.isConfirmed) submit();
        });
    });
})();

// Confirm