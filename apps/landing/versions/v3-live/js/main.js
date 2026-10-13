(() => {
  "use strict";

  const root = document.documentElement;
  const body = document.body;
  const themeToggle = document.querySelector("[data-theme-toggle]");
  const menuToggle = document.querySelector("[data-menu-toggle]");
  const mobileNav = document.querySelector("[data-mobile-nav]");
  const header = document.querySelector("[data-header]");
  const progress = document.querySelector(".scroll-progress span");
  const darkPreference = window.matchMedia("(prefers-color-scheme: dark)");
  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");

  root.classList.add("js");

  const syncTheme = () => {
    const dark = root.dataset.theme === "dark" ||
      (root.dataset.theme === "auto" && darkPreference.matches);
    root.dataset.effectiveTheme = dark ? "dark" : "light";
    themeToggle?.setAttribute("aria-pressed", String(dark));
    themeToggle?.setAttribute("aria-label", dark ? "Usar tema claro" : "Usar tema escuro");
  };
  syncTheme();
  darkPreference.addEventListener("change", syncTheme);
  themeToggle?.addEventListener("click", () => {
    root.dataset.theme = root.dataset.effectiveTheme === "dark" ? "light" : "dark";
    localStorage.setItem("prisma-workspace-theme", root.dataset.theme);
    syncTheme();
  });

  const closeMenu = () => {
    mobileNav?.classList.remove("is-open");
    body.classList.remove("menu-open");
    menuToggle?.setAttribute("aria-expanded", "false");
    menuToggle?.setAttribute("aria-label", "Abrir menu");
  };
  menuToggle?.addEventListener("click", () => {
    const open = !mobileNav?.classList.contains("is-open");
    mobileNav?.classList.toggle("is-open", open);
    body.classList.toggle("menu-open", open);
    menuToggle.setAttribute("aria-expanded", String(open));
    menuToggle.setAttribute("aria-label", open ? "Fechar menu" : "Abrir menu");
  });
  mobileNav?.querySelectorAll("a").forEach((link) => link.addEventListener("click", closeMenu));
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") closeMenu();
  });

  const updateScroll = () => {
    header?.classList.toggle("is-scrolled", window.scrollY > 24);
    const available = document.documentElement.scrollHeight - window.innerHeight;
    if (progress) progress.style.width = `${available > 0 ? window.scrollY / available * 100 : 0}%`;
  };
  updateScroll();
  window.addEventListener("scroll", updateScroll, { passive: true });
  window.addEventListener("resize", updateScroll, { passive: true });

  const revealItems = document.querySelectorAll(".reveal");
  if (reducedMotion.matches || !("IntersectionObserver" in window)) {
    revealItems.forEach((item) => item.classList.add("is-visible"));
  } else {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        entry.target.classList.add("is-visible");
        observer.unobserve(entry.target);
      });
    }, { rootMargin: "0px 0px -8% 0px", threshold: 0.08 });
    revealItems.forEach((item) => observer.observe(item));
  }

  const year = document.querySelector("[data-year]");
  if (year) year.textContent = String(new Date().getFullYear());

  const toast = document.querySelector("[data-link-toast]");
  let toastTimer;
  document.querySelectorAll('a[href="#"]').forEach((link) => {
    link.addEventListener("click", (event) => {
      event.preventDefault();
      if (!toast) return;
      toast.classList.add("is-visible");
      clearTimeout(toastTimer);
      toastTimer = setTimeout(() => toast.classList.remove("is-visible"), 3000);
    });
  });
})();
