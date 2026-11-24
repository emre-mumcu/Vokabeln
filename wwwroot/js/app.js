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

// Quick search
// Navbar'daki arama kutusuna yazdıkça /Words/QuickSearch'ten sonuçları alır ve altında açılır liste olarak gösterir.
// Sonuca tıklanınca kelimenin detay (View) sayfasına gidilir. Ara butonu/Enter tüm sonuçları listede gösterir.

(function () {
    const form = document.getElementById("quickSearchForm");
    const input = document.getElementById("quickSearch");
    const results = document.getElementById("quickSearchResults");
    if (!form || !input || !results) return;

    const minLength = 2;
    const delay = 250;
    let timer = null;
    let controller = null;

    function hide() {
        results.classList.remove("show");
    }

    function items() {
        return Array.from(results.querySelectorAll(".menu-item"));
    }

    function detailsUrl(id) {
        // Detay sayfasındaki "Geri" butonu, aramanın yapıldığı sayfaya dönsün
        const returnUrl = window.location.pathname + window.location.search;
        return input.dataset.detailsUrl + "/" + id + "?returnUrl=" + encodeURIComponent(returnUrl);
    }

    function render(list) {
        results.replaceChildren();

        if (list.length === 0) {
            const empty = document.createElement("span");
            empty.className = "menu-text";
            empty.textContent = "Sonuç bulunamadı";
            results.append(empty);
        }

        for (const item of list) {
            const link = document.createElement("a");
            link.className = "menu-item";
            link.href = detailsUrl(item.id);
            link.setAttribute("role", "option");

            const content = document.createElement("span");
            content.className = "menu-item-content";

            const word = document.createElement("span");
            word.textContent = item.word;
            content.append(word);

            if (item.meaning) {
                const meaning = document.createElement("span");
                meaning.className = "menu-item-description";
                meaning.textContent = item.meaning;
                content.append(meaning);
            }

            link.append(content);
            results.append(link);
        }

        results.classList.add("show");
    }

    async function search() {
        const q = input.value.trim();

        if (q.length < minLength) {
            hide();
            return;
        }

        // Önceki istek bitmediyse iptal edilir; eski sonuç yenisinin üzerine yazılmaz
        if (controller) controller.abort();
        controller = new AbortController();

        try {
            const response = await fetch(input.dataset.url + "?q=" + encodeURIComponent(q), {
                headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest" },
                signal: controller.signal
            });

            if (!response.ok) {
                hide();
                return;
            }

            render(await response.json());
        } catch (e) {
            // Oturum düştüyse yanıt JSON yerine giriş sayfası olur; liste sessizce kapanır
            if (e.name !== "AbortError") hide();
        }
    }

    input.addEventListener("input", function () {
        clearTimeout(timer);
        timer = setTimeout(search, delay);
    });

    input.addEventListener("focus", function () {
        if (input.value.trim().length >= minLength && results.childElementCount > 0) {
            results.classList.add("show");
        }
    });

    input.addEventListener("keydown", function (e) {
        if (e.key === "Escape") {
            hide();
        } else if (e.key === "ArrowDown" && results.classList.contains("show")) {
            const first = items()[0];
            if (first) {
                e.preventDefault();
                first.focus();
            }
        }
    });

    // Listede ok tuşlarıyla gezinme, Escape ile arama kutusuna dönüş
    results.addEventListener("keydown", function (e) {
        const list = items();
        const index = list.indexOf(document.activeElement);

        if (e.key === "ArrowDown" && index < list.length - 1) {
            e.preventDefault();
            list[index + 1].focus();
        } else if (e.key === "ArrowUp") {
            e.preventDefault();
            if (index > 0) list[index - 1].focus();
            else input.focus();
        } else if (e.key === "Escape") {
            hide();
            input.focus();
        }
    });

    // Arama alanı dışına tıklanınca liste kapanır
    document.addEventListener("click", function (e) {
        if (!form.contains(e.target)) hide();
    });
})();

// Quick search