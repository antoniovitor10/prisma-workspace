// Gema 3D do hero — three.js. Falha graciosamente para o SVG fallback.
import * as THREE from "three";

const root = document.documentElement;
const canvas = document.querySelector(".gem-canvas");
const reduce = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
const smallCoarse =
  window.matchMedia("(pointer: coarse)").matches && window.innerWidth < 720;

// Suporte a WebGL
function hasWebGL() {
  try {
    const c = document.createElement("canvas");
    return !!(window.WebGLRenderingContext && (c.getContext("webgl") || c.getContext("experimental-webgl")));
  } catch (_) {
    return false;
  }
}

if (!canvas || reduce || smallCoarse || !hasWebGL()) {
  // mantém o SVG fallback visível
} else {
  init();
}

function init() {
  const stage = canvas.parentElement;
  let w = stage.clientWidth;
  let h = stage.clientHeight;

  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, powerPreference: "high-performance" });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.5));
  renderer.setSize(w, h, false);

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(38, w / h, 0.1, 100);
  camera.position.set(0, 0, 6.2);

  // ---- gema: bipirâmide (octaedro alongado em Y), facetas coloridas ----
  const geo = new THREE.OctahedronGeometry(1.5, 0);
  geo.scale(1, 1.42, 1);
  geo.translate(0, -0.1, 0);

  // cor por face, seguindo o espectro da marca
  const palette = [
    new THREE.Color("#7C3AED"), // violeta
    new THREE.Color("#2563EB"), // azul
    new THREE.Color("#A855F7"), // roxo
    new THREE.Color("#3B82F6"), // azul claro
    new THREE.Color("#DB2777"), // magenta
    new THREE.Color("#6D28D9"), // violeta escuro
    new THREE.Color("#1D4ED8"), // azul profundo
    new THREE.Color("#F97316"), // laranja (ponta)
  ];
  const nonIndexed = geo.toNonIndexed();
  const pos = nonIndexed.getAttribute("position");
  const colors = [];
  const faceCount = pos.count / 3;
  for (let f = 0; f < faceCount; f++) {
    // ponta inferior recebe magenta/laranja; resto varia pelo espectro
    let color;
    const y0 = pos.getY(f * 3);
    const y1 = pos.getY(f * 3 + 1);
    const y2 = pos.getY(f * 3 + 2);
    const low = Math.min(y0, y1, y2);
    if (low < -1.4) color = palette[f % 2 ? 4 : 7];
    else color = palette[f % palette.length];
    for (let v = 0; v < 3; v++) colors.push(color.r, color.g, color.b);
  }
  nonIndexed.setAttribute("color", new THREE.Float32BufferAttribute(colors, 3));

  const mat = new THREE.MeshStandardMaterial({
    vertexColors: true,
    flatShading: true,
    metalness: 0.18,
    roughness: 0.32,
    emissive: new THREE.Color("#7C3AED"),
    emissiveIntensity: 0.22,
  });
  const gem = new THREE.Mesh(nonIndexed, mat);
  scene.add(gem);

  // aresta luminosa
  const edges = new THREE.LineSegments(
    new THREE.EdgesGeometry(nonIndexed, 1),
    new THREE.LineBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.28 })
  );
  gem.add(edges);

  // ---- luzes ----
  scene.add(new THREE.AmbientLight(0xb8c4ff, 0.95));
  const key = new THREE.DirectionalLight(0xffffff, 2.1);
  key.position.set(3, 4, 5);
  scene.add(key);
  const rimV = new THREE.PointLight(0x8b5cf6, 9, 20);
  rimV.position.set(-4, 2, 3);
  scene.add(rimV);
  const rimO = new THREE.PointLight(0xf97316, 9, 20);
  rimO.position.set(3, -4, 2);
  scene.add(rimO);
  const rimP = new THREE.PointLight(0xdb2777, 7, 20);
  rimP.position.set(4, -1, 3);
  scene.add(rimP);
  const fill = new THREE.DirectionalLight(0x60a5fa, 0.8);
  fill.position.set(-2, -1, 4);
  scene.add(fill);

  // ---- interação: tilt no ponteiro ----
  const target = { x: 0, y: 0 };
  const cur = { x: 0, y: 0 };
  window.addEventListener(
    "pointermove",
    (e) => {
      target.x = (e.clientX / window.innerWidth - 0.5) * 0.6;
      target.y = (e.clientY / window.innerHeight - 0.5) * 0.6;
    },
    { passive: true }
  );

  function resize() {
    w = stage.clientWidth;
    h = stage.clientHeight;
    if (w === 0 || h === 0) return;
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
  }
  window.addEventListener("resize", resize, { passive: true });

  root.classList.add("gem-on");

  let t = 0;
  let running = true;
  // pausa quando o hero sai da tela (economia)
  if ("IntersectionObserver" in window) {
    new IntersectionObserver(
      (entries) => entries.forEach((en) => (running = en.isIntersecting)),
      { threshold: 0 }
    ).observe(stage);
  }

  const clock = new THREE.Clock();
  let acc = 0;
  const frame = 1 / 30; // limita a ~30fps para aliviar o thread
  function loop() {
    requestAnimationFrame(loop);
    if (!running) return;
    const dt = clock.getDelta();
    acc += dt;
    if (acc < frame) return;
    acc = 0;
    t += dt;
    cur.x += (target.x - cur.x) * 0.08;
    cur.y += (target.y - cur.y) * 0.08;
    gem.rotation.y += dt * 0.35;
    gem.rotation.x = cur.y * 0.8 + Math.sin(t * 0.6) * 0.04;
    gem.rotation.z = cur.x * -0.3;
    gem.position.y = Math.sin(t * 0.8) * 0.08;
    renderer.render(scene, camera);
  }
  // garante tamanho correto após layout
  requestAnimationFrame(() => {
    resize();
    loop();
  });
}
