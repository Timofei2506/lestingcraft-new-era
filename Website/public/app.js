// LestingCraft: New Era site. No inline scripts (CSP script-src 'self').
(() => {
  const root = document.documentElement;
  root.classList.remove('no-js');
  const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');

  // ---------- Nav ----------
  const nav = document.getElementById('nav');
  if (nav) {
    const onScroll = () => nav.classList.toggle('scrolled', window.scrollY > 24);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
  }
  const toggle = document.getElementById('menu-toggle');
  const links = document.getElementById('nav-links');
  const setMenu = (open) => {
    links.classList.toggle('open', open);
    toggle.setAttribute('aria-expanded', String(open));
  };
  if (toggle && links) {
    toggle.addEventListener('click', () => setMenu(!links.classList.contains('open')));
    links.addEventListener('click', (e) => { if (e.target.closest('a')) setMenu(false); });
  }
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape' && links) setMenu(false); });

  // ---------- Reveal on scroll ----------
  const revealItems = document.querySelectorAll('.reveal');
  if ('IntersectionObserver' in window && !reduceMotion.matches) {
    const io = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          entry.target.classList.add('in');
          io.unobserve(entry.target);
        }
      }
    }, { rootMargin: '0px 0px -8% 0px', threshold: 0.08 });
    revealItems.forEach((el, i) => {
      el.dataset.delay = String(i % 3 === 0 ? 0 : (i % 3));
      io.observe(el);
    });
  } else {
    revealItems.forEach((el) => el.classList.add('in'));
  }

  // ---------- Server status (honest: unknown until the API answers) ----------
  const status = document.getElementById('server-status');
  const statusText = status ? status.querySelector('span') : null;
  if (status) fetch('/api/status', { headers: { Accept: 'application/json' } })
    .then((r) => (r.ok ? r.json() : Promise.reject(new Error(String(r.status)))))
    .then((d) => {
      if (d && d.online === true) {
        status.dataset.state = 'online';
        statusText.textContent = 'Сервер онлайн';
      } else {
        status.dataset.state = 'offline';
        statusText.textContent = 'Сервер офлайн';
      }
    })
    .catch(() => {
      status.dataset.state = 'unknown';
      statusText.textContent = 'Статус недоступен';
    });

  // ---------- Gallery lightbox ----------
  const dialog = document.getElementById('lightbox');
  const lbImg = document.getElementById('lb-img');
  const lbCaption = document.getElementById('lb-caption');
  if (dialog) document.querySelectorAll('[data-shot]').forEach((btn) => {
    btn.addEventListener('click', () => {
      const img = btn.querySelector('img');
      lbImg.src = btn.dataset.shot;
      lbImg.alt = img ? img.alt : '';
      lbCaption.textContent = btn.dataset.caption || '';
      if (typeof dialog.showModal === 'function') dialog.showModal();
    });
  });
  if (dialog) {
    document.getElementById('lb-close').addEventListener('click', () => dialog.close());
    dialog.addEventListener('click', (e) => { if (e.target === dialog) dialog.close(); });
    dialog.addEventListener('close', () => { lbImg.removeAttribute('src'); });
  }

  // ---------- Waitlist form ----------
  const form = document.getElementById('waitlist');
  const message = document.getElementById('form-status');
  const submit = form ? form.querySelector('button[type="submit"]') : null;
  const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;
  const say = (text, kind) => { message.textContent = text; message.dataset.kind = kind; };

  if (form) form.addEventListener('submit', async (e) => {
    e.preventDefault();
    const data = new FormData(form);
    const email = String(data.get('email') || '').trim();
    if (!EMAIL.test(email) || email.length > 254) {
      say('Проверьте адрес почты: он выглядит неполным.', 'error');
      form.querySelector('input[name="email"]').focus();
      return;
    }
    if (!form.querySelector('input[name="consent"]').checked) {
      say('Нужно согласие на хранение адреса, чтобы записать вас.', 'error');
      return;
    }
    form.dataset.busy = 'true';
    submit.textContent = 'Отправляем…';
    say('', '');
    try {
      const res = await fetch('/api/waitlist', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify({
          email,
          consent: true,
          website: String(data.get('website') || ''),
          note: String(data.get('note') || '').slice(0, 500),
          locale: navigator.language || 'ru',
        }),
      });
      const body = await res.json().catch(() => ({}));
      if (res.ok && body.ok) {
        say(body.message || 'Готово! Напишем, когда откроем тестовый доступ.', 'ok');
        form.reset();
      } else if (res.status === 503) {
        say(body.message || 'Лист ожидания скоро откроется. Попробуйте позже.', 'soon');
      } else {
        say(body.message || 'Не получилось записать адрес. Попробуйте ещё раз.', 'error');
      }
    } catch {
      say('Нет связи с сервером. Проверьте соединение и попробуйте ещё раз.', 'error');
    } finally {
      form.dataset.busy = 'false';
      submit.textContent = 'Записаться';
    }
  });

  // ---------- Voxel landscape background ----------
  const canvas = document.getElementById('voxel-bg');
  if (canvas && canvas.getContext) {
    const ctx = canvas.getContext('2d');
    const COLS = 26, ROWS = 18;
    let width = 0, height = 0, dpr = 1, tw = 40, th = 20, hz = 16;
    let mouseX = 0.5, targetMouse = 0.5;
    let visible = true, rafId = 0, t0 = performance.now();

    const hash = (c, r) => {
      const v = Math.sin(c * 127.1 + r * 311.7) * 43758.5453;
      return v - Math.floor(v);
    };
    const level = (c, r, t) => {
      const wave = Math.sin(c * 0.42 + t * 0.35) + Math.cos(r * 0.51 - t * 0.27) + 0.6 * Math.sin((c + r) * 0.23 + t * 0.18);
      return Math.max(0, Math.round(2.2 + wave * 1.7 + hash(c, r) * 0.6));
    };
    const shade = (base, dl) => {
      const [h, s, l] = base;
      return `hsl(${h} ${s}% ${Math.max(4, Math.min(92, l + dl))}%)`;
    };
    const poly = (pts, fill) => {
      ctx.beginPath();
      ctx.moveTo(pts[0][0], pts[0][1]);
      for (let i = 1; i < pts.length; i++) ctx.lineTo(pts[i][0], pts[i][1]);
      ctx.closePath();
      ctx.fillStyle = fill;
      ctx.fill();
    };

    const resize = () => {
      const rect = canvas.getBoundingClientRect();
      width = rect.width;
      height = rect.height;
      dpr = Math.min(window.devicePixelRatio || 1, 1.5);
      canvas.width = Math.round(width * dpr);
      canvas.height = Math.round(height * dpr);
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      tw = Math.max(26, Math.min(64, width / 19));
      th = tw / 2;
      hz = tw * 0.46;
    };

    const draw = (time) => {
      mouseX += (targetMouse - mouseX) * 0.04;
      ctx.clearRect(0, 0, width, height);
      const ox = width * 0.66 + (mouseX - 0.5) * -40;
      const oy = height * 0.42 - ROWS * th * 0.25;
      for (let s = 0; s <= COLS + ROWS - 2; s++) {
        for (let c = 0; c < COLS; c++) {
          const r = s - c;
          if (r < 0 || r >= ROWS) continue;
          const z = level(c, r, time);
          const X = (c - r) * tw / 2 + ox;
          const Y = (c + r) * th / 2 + oy;
          const k = z / 6;
          const hue = 96 - k * 30;
          const sat = 34 + k * 20;
          const light = 20 + k * 22 + hash(c, r) * 3;
          const top = [hue, sat, light];
          const topPts = [[X, Y - z * hz], [X + tw / 2, Y + th / 2 - z * hz], [X, Y + th - z * hz], [X - tw / 2, Y + th / 2 - z * hz]];
          const left = [[X - tw / 2, Y + th / 2 - z * hz], [X, Y + th - z * hz], [X, Y + th], [X - tw / 2, Y + th / 2]];
          const right = [[X, Y + th - z * hz], [X + tw / 2, Y + th / 2 - z * hz], [X + tw / 2, Y + th / 2], [X, Y + th]];
          const warm = k > 0.7 ? 'hsla(40 90% 62% / 0.18)' : null;
          poly(left, shade(top, -12));
          poly(right, shade(top, -6));
          poly(topPts, shade(top, 0));
          if (warm) poly(topPts, warm);
        }
      }
    };

    const loop = (now) => {
      draw((now - t0) / 1000);
      if (visible && !document.hidden && !reduceMotion.matches) rafId = requestAnimationFrame(loop);
      else rafId = 0;
    };
    const start = () => { if (!rafId && visible && !document.hidden && !reduceMotion.matches) rafId = requestAnimationFrame(loop); };

    resize();
    draw(0);
    window.addEventListener('resize', () => { resize(); draw((performance.now() - t0) / 1000); });
    window.addEventListener('pointermove', (e) => { targetMouse = e.clientX / window.innerWidth; }, { passive: true });
    document.addEventListener('visibilitychange', start);
    reduceMotion.addEventListener?.('change', () => { if (!reduceMotion.matches) start(); });
    if ('IntersectionObserver' in window) {
      new IntersectionObserver((entries) => {
        visible = entries[0].isIntersecting;
        start();
      }).observe(canvas);
    }
    start();
  }
})();
