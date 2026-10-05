(() => {
  "use strict";

  const root = document.documentElement;
  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
  const systemDark = window.matchMedia("(prefers-color-scheme: dark)");
  const themeColor = document.querySelector('meta[name="theme-color"]');

  // This archived page has no copy of its original entrance animation.
  // Keep the static content visible while the shared controls run.

  /* ---------- tema: preferência automática + escolha persistida ---------- */
  const preferredTheme = () => "dark"; // dark-first: combina com o hero espacial
  const storedTheme = localStorage.getItem("prisma-theme");
  root.dataset.theme = storedTheme === "dark" || storedTheme === "light"
    ? storedTheme
    : preferredTheme();

  const themeToggle = document.querySelector(".theme-toggle");
  const syncThemeControls = () => {
    const isDark = root.dataset.theme === "dark";
    if (themeToggle) {
      themeToggle.setAttribute("aria-label", isDark ? "Usar tema claro" : "Usar tema escuro");
      themeToggle.setAttribute("title", isDark ? "Usar tema claro" : "Usar tema escuro");
    }
    if (themeColor) {
      themeColor.setAttribute("content", isDark ? "#020617" : "#F4F0FF");
    }
  };

  syncThemeControls();

  themeToggle?.addEventListener("click", () => {
    root.dataset.theme = root.dataset.theme === "dark" ? "light" : "dark";
    localStorage.setItem("prisma-theme", root.dataset.theme);
    syncThemeControls();
  });

  systemDark.addEventListener("change", () => {
    if (localStorage.getItem("prisma-theme")) return;
    root.dataset.theme = preferredTheme();
    syncThemeControls();
  });

  /* ---------- nav: estado de scroll ---------- */
  const nav = document.querySelector(".nav");
  let ticking = false;

  const paintScrollState = () => {
    const scrollTop = window.scrollY;
    const max = document.documentElement.scrollHeight - window.innerHeight;
    const progress = max > 0 ? scrollTop / max : 0;
    const progressBar = document.querySelector(".beam-progress");

    nav?.classList.toggle("is-scrolled", scrollTop > 24);
    if (progressBar) progressBar.style.transform = `scaleX(${progress})`;
    ticking = false;
  };

  const requestPaint = () => {
    if (ticking) return;
    ticking = true;
    requestAnimationFrame(paintScrollState);
  };

  paintScrollState();
  window.addEventListener("scroll", requestPaint, { passive: true });
  window.addEventListener("resize", requestPaint, { passive: true });

  /* ---------- nav: menu mobile ---------- */
  const menuToggle = document.querySelector(".nav-toggle");
  const closeMenu = () => {
    nav?.classList.remove("menu-open");
    menuToggle?.setAttribute("aria-expanded", "false");
    menuToggle?.setAttribute("aria-label", "Abrir menu");
  };

  menuToggle?.addEventListener("click", () => {
    const open = nav?.classList.toggle("menu-open") ?? false;
    menuToggle.setAttribute("aria-expanded", String(open));
    menuToggle.setAttribute("aria-label", open ? "Fechar menu" : "Abrir menu");
  });

  document.querySelectorAll(".nav-links a").forEach((link) => {
    link.addEventListener("click", closeMenu);
  });

  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape") closeMenu();
  });

  /* Links marcados como pendentes ficam visualmente prontos sem navegar. */
  document.querySelectorAll('a[href="#"]').forEach((link) => {
    link.addEventListener("click", (event) => event.preventDefault());
  });

  /* ---------- entrada do hero e reveals sutis ---------- */
  requestAnimationFrame(() => {
    requestAnimationFrame(() => root.classList.add("is-ready"));
  });

  const revealTargets = document.querySelectorAll(
    ".section-head, .feat, .product__stage, .os-item, .ed-card, .srv-item, " +
    ".tech-grid li, .rm-item, .faq-item, .cta-box"
  );

  revealTargets.forEach((element) => element.setAttribute("data-reveal", ""));

  if (reducedMotion.matches || !("IntersectionObserver" in window)) {
    revealTargets.forEach((element) => element.classList.add("is-visible"));
  } else {
    const revealObserver = new IntersectionObserver(
      (entries, observer) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) return;
          entry.target.classList.add("is-visible");
          observer.unobserve(entry.target);
        });
      },
      { rootMargin: "0px 0px -10% 0px", threshold: 0.08 }
    );
    revealTargets.forEach((element) => revealObserver.observe(element));
  }

  /* ---------- recursos: acordeão + painel de mídia sincronizado ---------- */
  const featItems = [...document.querySelectorAll(".feat__item")];
  const featPanels = [...document.querySelectorAll(".feat__panel")];

  const openFeat = (item) => {
    featItems.forEach((el) => {
      const open = el === item;
      el.classList.toggle("is-open", open);
      el.querySelector(".feat__head")?.setAttribute("aria-expanded", String(open));
    });
    const key = item.dataset.feat;
    featPanels.forEach((p) => p.classList.toggle("is-active", p.dataset.feat === key));
  };

  featItems.forEach((item) => {
    item.querySelector(".feat__head")?.addEventListener("click", () => openFeat(item));
  });

  /* ---------- gema: parallax leve seguindo o ponteiro (sem 3D) ---------- */
  const gemStage = document.querySelector(".gem-stage");
  if (gemStage && !reducedMotion.matches && window.matchMedia("(pointer: fine)").matches) {
    let tx = 0, ty = 0, cx = 0, cy = 0, raf = null;
    const tick = () => {
      cx += (tx - cx) * 0.08;
      cy += (ty - cy) * 0.08;
      gemStage.style.transform =
        `translate(${(cx * 16).toFixed(2)}px, ${(cy * 16).toFixed(2)}px) rotate(${(cx * 3).toFixed(2)}deg)`;
      raf = Math.abs(tx - cx) > 0.001 || Math.abs(ty - cy) > 0.001 ? requestAnimationFrame(tick) : null;
    };
    window.addEventListener("pointermove", (e) => {
      tx = e.clientX / window.innerWidth - 0.5;
      ty = e.clientY / window.innerHeight - 0.5;
      if (!raf) raf = requestAnimationFrame(tick);
    }, { passive: true });
  }

  /* ---------- movimento: GSAP ScrollTrigger (scroll nativo, sem hijack) ---------- */
  const gsap = window.gsap;
  const ScrollTrigger = window.ScrollTrigger;

  if (!reducedMotion.matches && gsap && ScrollTrigger) {
    gsap.registerPlugin(ScrollTrigger);

    /* card que cresce até virar o fundo (assinatura typebot) */
    const grow = document.querySelector(".grow");
    const card = document.querySelector(".grow__card");
    const content = document.querySelector(".grow__content");
    if (grow && card) {
      const tl = gsap.timeline({
        scrollTrigger: {
          trigger: grow,
          start: "top top",
          end: "bottom bottom",
          scrub: 0.6,
        },
      });
      tl.fromTo(
        card,
        { scale: 0.26, borderRadius: "28px", autoAlpha: 0.9 },
        { scale: 1, borderRadius: "0px", autoAlpha: 1, duration: 0.6, ease: "power2.inOut" }
      )
        .fromTo(
          content,
          { autoAlpha: 0, y: 30 },
          { autoAlpha: 1, y: 0, duration: 0.3, ease: "power2.out" },
          0.42
        )
        .to({}, { duration: 0.4 }); // segura o card cheio com a frase visível
    }

    /* parallax leve de profundidade (mockups do produto) */
    document.querySelectorAll("[data-parallax]").forEach((el) => {
      const dist = parseFloat(el.getAttribute("data-parallax")) || 0;
      gsap.to(el, {
        y: dist,
        ease: "none",
        scrollTrigger: { trigger: el.closest(".product"), start: "top bottom", end: "bottom top", scrub: 0.8 },
      });
    });

    ScrollTrigger.refresh();
  }
})();
