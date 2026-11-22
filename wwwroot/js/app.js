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