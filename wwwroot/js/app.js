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
        // Detay sayfasındaki "Geri" butonu, aramanın yapıldığı sayfaya dönsün.
        // Detay sayfasından aranırsa o sayfanın kendi returnUrl'i aktarılır: zincir oluşmaz, Geri ilk listeye döner.
        let returnUrl = window.location.pathname + window.location.search;

        if (window.location.pathname.toLowerCase().startsWith(input.dataset.detailsUrl.toLowerCase() + "/")) {
            const previous = new URLSearchParams(window.location.search).get("returnUrl");

            // Yalnızca site içi adres ("/..." ama "//..." değil); yoksa Geri varsayılan olarak kelime listesine döner
            returnUrl = previous && previous.startsWith("/") && !previous.startsWith("//") ? previous : null;
        }

        const url = input.dataset.detailsUrl + "/" + id;
        return returnUrl ? url + "?returnUrl=" + encodeURIComponent(returnUrl) : url;
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

// Wiktionary
// Kelime ve çekim formlarındaki "Wiktionary'den getir" butonu için ortak yardımcılar.
// Veri yerel kaynak dosyasından (wiktionary-de.json) /Wiktionary/Lookup ile gelir; formu doldurma işi her sayfanın kendisindedir.

window.Wiktionary = (function () {
    const hasSwal = function () { return typeof Swal !== "undefined"; };

    function message(icon, text) {
        if (hasSwal()) return Swal.fire({ icon: icon, text: text });
        window.alert(text);
        return Promise.resolve();
    }

    // Aday satırı: "gehen (das Verb) ← ging: 1. Person ... — gitmek"
    function label(entry) {
        let text = entry.word + (entry.wordTypeText ? " (" + entry.wordTypeText + ")" : "");
        if (entry.via) text += " ← " + entry.via;
        const hint = entry.meaning || (entry.glosses.length > 0 ? entry.glosses[0] : "");
        if (hint) text += " — " + hint;
        return text.length > 160 ? text.substring(0, 157) + "..." : text;
    }

    // Kelimeyi arar; birden fazla sonuç varsa seçtirir. Seçilen maddeyi (ya da null) döndürür.
    async function find(button, query, type) {
        if (!query || !query.trim()) {
            await message("info", "Önce aranacak kelimeyi yazın.");
            return null;
        }

        const url = button.dataset.wiktionaryUrl + "?q=" + encodeURIComponent(query.trim()) +
            (type !== null && type !== undefined && type !== "" ? "&type=" + encodeURIComponent(type) : "");

        // İlk aramada kaynak dosya indekslenir (birkaç saniye sürebilir)
        const original = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<i class="bi bi-hourglass-split"></i> Aranıyor...';

        let result;
        try {
            const response = await fetch(url, { headers: { "Accept": "application/json" } });
            if (!response.ok) throw new Error(response.status);
            result = await response.json();
        } catch (e) {
            await message("error", "Wiktionary kaynağı okunamadı.");
            return null;
        } finally {
            button.disabled = false;
            button.innerHTML = original;
        }

        if (!result.available || result.entries.length === 0) {
            await message(result.available ? "info" : "error", result.message || "Bulunamadı.");
            return null;
        }

        if (result.entries.length === 1 || !hasSwal()) return result.entries[0];

        const options = {};
        result.entries.forEach(function (entry, i) { options[i] = label(entry); });

        const choice = await Swal.fire({
            title: "Hangisi?",
            text: result.entries.length + " sonuç bulundu.",
            input: "radio",
            inputOptions: options,
            inputValue: "0",
            showCancelButton: true,
            confirmButtonText: "Getir",
            cancelButtonText: "Vazgeç",
            customClass: { input: "text-start" }
        });

        return choice.isConfirmed ? result.entries[parseInt(choice.value, 10)] : null;
    }

    // Dolu alanlar farklı bir değerle değişecekse sorar: "all" (üzerine yaz), "empty" (yalnızca boşlar) ya da null (vazgeç)
    async function askOverwrite(conflicts) {
        if (conflicts.length === 0) return "all";

        if (!hasSwal()) return window.confirm("Dolu alanların üzerine yazılsın mı? (" + conflicts.join(", ") + ")") ? "all" : "empty";

        const result = await Swal.fire({
            icon: "question",
            title: "Dolu alanlar var",
            text: "Şu alanlar zaten dolu: " + conflicts.join(", ") + ". Wiktionary'deki değerler yazılsın mı?",
            showDenyButton: true,
            showCancelButton: true,
            confirmButtonText: "Üzerine yaz",
            denyButtonText: "Yalnızca boş alanlar",
            cancelButtonText: "Vazgeç"
        });

        if (result.isConfirmed) return "all";
        if (result.isDenied) return "empty";
        return null;
    }

    // Doldurulan alanları ve (showHelp false değilse) Türkçe anlam yoksa Almanca açıklama / İngilizce karşılıkları gösterir
    function summary(entry, filled, showHelp) {
        const box = document.createElement("div");
        box.className = "text-start";

        const p = document.createElement("p");
        p.textContent = filled.length > 0 ? "Doldurulan alanlar: " + filled.join(", ") + "." : "Değişen alan olmadı.";
        box.appendChild(p);

        if (showHelp !== false && !entry.meaning && (entry.glosses.length > 0 || entry.english.length > 0)) {
            const note = document.createElement("p");
            note.className = "fg-secondary mb-1";
            note.textContent = "Kaynakta Türkçe anlam yok. Yardımcı olsun diye:";
            box.appendChild(note);

            const list = document.createElement("ul");
            list.className = "small mb-0";
            entry.glosses.forEach(function (g) {
                const li = document.createElement("li");
                li.textContent = g;
                list.appendChild(li);
            });
            if (entry.english.length > 0) {
                const li = document.createElement("li");
                li.textContent = "EN: " + entry.english.join(", ");
                list.appendChild(li);
            }
            box.appendChild(list);
        }

        if (!hasSwal()) return;
        Swal.fire({ icon: "success", title: entry.word, html: box });
    }

    return { find: find, askOverwrite: askOverwrite, summary: summary };
})();

// Wiktionary
