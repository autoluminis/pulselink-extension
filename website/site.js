const slides = [...document.querySelectorAll('.slide')];
const links = [...document.querySelectorAll('.nav-links a,.deck-rail a')];
const prev = document.getElementById('prev-slide');
const next = document.getElementById('next-slide');
const counter = document.querySelector('.deck-count');
const menuButton = document.querySelector('.menu-toggle');
const menu = document.querySelector('.nav-links');
const guide = document.getElementById('guide-dialog');
const reduced = matchMedia('(prefers-reduced-motion: reduce)');
let current = 0;
let animation = null;
let wheelUntil = 0;
let wheelSum = 0;
let lastWheel = 0;
const aliases = {connect:'connections', download:'downloads', start:'manual', faq:'manual', sdk:'manual'};
function headerHeight() { return parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--header')); }
function closeMenu() { menu.classList.remove('is-open'); menuButton.setAttribute('aria-expanded', 'false'); }
function mark(index) {
  current = index;
  slides.forEach((slide, i) => slide.classList.toggle('is-active', i === index));
  const hash = '#' + slides[index].id;
  links.forEach(link => { if (link.hash === hash) link.setAttribute('aria-current', 'true'); else link.removeAttribute('aria-current'); });
  document.querySelector('.current-chapter').textContent = document.querySelector('.nav-links a[href="' + hash + '"]').textContent;
  prev.disabled = index === 0;
  next.disabled = index === slides.length - 1;
  counter.textContent = String(index + 1).padStart(2, '0') + ' / ' + String(slides.length).padStart(2, '0');
  if (location.hash !== hash) history.replaceState(null, '', hash);
}
// Keep one animation owner so browser snapping and repeated input cannot fight the transition.
function cancelAnimation() {
  if (!animation) return;
  cancelAnimationFrame(animation.frame);
  animation = null;
  document.documentElement.classList.remove('is-transitioning');
}
function go(index, pushHistory = false) {
  index = Math.max(0, Math.min(slides.length - 1, index));
  if (animation?.index === index) return;
  cancelAnimation();
  const hash = '#' + slides[index].id;
  if (pushHistory && location.hash !== hash) history.pushState(null, '', hash);
  const start = scrollY;
  const max = Math.max(0, document.documentElement.scrollHeight - innerHeight);
  const target = Math.max(0, Math.min(max, start + slides[index].getBoundingClientRect().top - headerHeight()));
  const distance = target - start;
  if (reduced.matches || Math.abs(distance) < 2) {
    scrollTo({top:target, behavior:'instant'});
    mark(index);
    wheelUntil = performance.now() + 200;
    return;
  }
  document.documentElement.classList.add('is-transitioning');
  const duration = Math.min(1050, 720 + Math.abs(distance) * .07);
  const started = performance.now();
  const state = {index, frame:0};
  animation = state;
  function tick(now) {
    if (animation !== state) return;
    const t = Math.min(1, (now - started) / duration);
    // Smooth acceleration and deceleration, with no overshoot at the resting position.
    const eased = t * t * t * (t * (t * 6 - 15) + 10);
    scrollTo({top:start + distance * eased, behavior:'instant'});
    if (t < 1) state.frame = requestAnimationFrame(tick);
    else {
      scrollTo({top:target, behavior:'instant'});
      animation = null;
      document.documentElement.classList.remove('is-transitioning');
      mark(index);
      wheelUntil = now + 220;
    }
  }
  state.frame = requestAnimationFrame(tick);
}
menuButton.addEventListener('click', () => {
  const open = menuButton.getAttribute('aria-expanded') !== 'true';
  menu.classList.toggle('is-open', open);
  menuButton.setAttribute('aria-expanded', String(open));
});
document.querySelectorAll('a[href^="#"]').forEach(link => link.addEventListener('click', event => {
  const id = aliases[link.hash.slice(1)] || link.hash.slice(1);
  const index = slides.findIndex(slide => slide.id === id);
  if (index < 0) return;
  event.preventDefault();
  closeMenu();
  go(index, true);
}));
prev.addEventListener('click', () => go(current - 1));
next.addEventListener('click', () => go(current + 1));
let observeFrame = 0;
function observePosition() {
  if (observeFrame) return;
  observeFrame = requestAnimationFrame(() => {
    observeFrame = 0;
    if (animation) return;
    const middle = headerHeight() + (innerHeight - headerHeight()) * .42;
    let index = slides.findIndex(slide => { const r = slide.getBoundingClientRect(); return r.top <= middle && r.bottom > middle; });
    if (index < 0) index = current;
    if (index !== current) mark(index);
  });
}
addEventListener('scroll', observePosition, {passive:true});
addEventListener('wheel', event => {
  if (innerWidth <= 760 || document.querySelector('dialog[open]') || menu.classList.contains('is-open') || event.ctrlKey || Math.abs(event.deltaX) > Math.abs(event.deltaY)) return;
  if (event.target.closest('dialog,iframe,input,textarea,select,[contenteditable]')) return;
  const delta = event.deltaY * (event.deltaMode === 1 ? 16 : event.deltaMode === 2 ? innerHeight : 1);
  if (!delta) return;
  const now = performance.now();
  const r = slides[current].getBoundingClientRect();
  const hasOverflow = r.height > innerHeight - headerHeight() + 3;
  if (!animation && hasOverflow && ((delta > 0 && r.bottom > innerHeight + 3) || (delta < 0 && r.top < headerHeight() - 3))) return;
  event.preventDefault();
  if (animation || now < wheelUntil) { if (!animation) wheelUntil = now + 140; lastWheel = now; return; }
  if (now - lastWheel > 160 || Math.sign(delta) !== Math.sign(wheelSum)) wheelSum = 0;
  lastWheel = now;
  wheelSum += delta;
  if (Math.abs(wheelSum) < 38) return;
  const direction = Math.sign(wheelSum);
  wheelSum = 0;
  go(current + direction);
}, {passive:false});
// Direct touch or a viewport resize should immediately give control back to native scrolling.
addEventListener('touchstart', cancelAnimation, {passive:true});
addEventListener('resize', () => { cancelAnimation(); observePosition(); });
document.getElementById('open-guide').addEventListener('click', () => {
  cancelAnimation();
  const frame = guide.querySelector('iframe');
  if (!frame.getAttribute('src')) frame.src = 'assets/PulseLink-User-Guide.pdf#view=FitH';
  guide.showModal();
});
document.getElementById('close-guide').addEventListener('click', () => guide.close());
guide.addEventListener('click', event => {
  if (event.target !== guide) return;
  const r = guide.getBoundingClientRect();
  if (event.clientX < r.left || event.clientX > r.right || event.clientY < r.top || event.clientY > r.bottom) guide.close();
});
document.addEventListener('keydown', event => {
  if (event.key === 'Escape') { closeMenu(); cancelAnimation(); return; }
  if (document.querySelector('dialog[open]') || menu.classList.contains('is-open') || event.altKey || event.ctrlKey || event.metaKey || event.target.closest('a,button,input,textarea,select,[contenteditable],iframe')) return;
  if (['ArrowDown','PageDown'].includes(event.key)) { event.preventDefault(); if (!animation) go(current + 1); }
  else if (['ArrowUp','PageUp'].includes(event.key)) { event.preventDefault(); if (!animation) go(current - 1); }
  else if (event.key === 'Home') { event.preventDefault(); go(0); }
  else if (event.key === 'End') { event.preventDefault(); go(slides.length - 1); }
});
function restoreHash() {
  const id = aliases[location.hash.slice(1)] || location.hash.slice(1);
  const index = slides.findIndex(slide => slide.id === id);
  go(index >= 0 ? index : 0);
}
addEventListener('popstate', restoreHash);
const initial = aliases[location.hash.slice(1)] || location.hash.slice(1);
const initialIndex = Math.max(0, slides.findIndex(slide => slide.id === initial));
slides[initialIndex].scrollIntoView({behavior:'instant', block:'start'});
mark(initialIndex);

document.addEventListener('pulselink:modal-open', cancelAnimation);
