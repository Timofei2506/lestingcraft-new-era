// LestingCraft: New Era — Flagship Site Behaviour (strict CSP: script-src 'self', zero dependencies)
(() => {
  'use strict';

  const root = document.documentElement;
  root.classList.remove('no-js');
  const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
  const reduced = reduceMotion.matches;

  // ---------- Honest visit counter (aggregate only, no cookies) ----------
  (() => {
    const allowed = new Set(['direct', 'github', 'share', 'search', 'friend', 'press']);
    const pick = () => {
      const params = new URLSearchParams(location.search);
      const raw = (params.get('ref') || params.get('utm_source') || '').toLowerCase().replace(/[^a-z_-]/g, '').slice(0, 16);
      if (allowed.has(raw)) return raw;
      const referrer = document.referrer || '';
      let host = '';
      try { host = referrer ? new URL(referrer).hostname : ''; } catch { host = ''; }
      if (!host) return 'direct';
      if (host.endsWith('github.com') || host.endsWith('githubusercontent.com') || host.endsWith('github.io')) return 'github';
      if (/(^|\.)(ya\.ru|yandex|bing\.com|google\.|duckduckgo|mail\.ru)$/.test(host) || /yandex|google|bing|duckduckgo/.test(host)) return 'search';
      if (host.endsWith('lestingcraft.kroterallitir.workers.dev')) return 'direct';
      return 'friend';
    };
    const day = new Date().toISOString().slice(0, 10);
    const key = `lc_hit_${day}`;
    try {
      if (sessionStorage.getItem(key)) return;
      sessionStorage.setItem(key, '1');
    } catch { /* private mode: count once per load */ }
    fetch('/api/hit', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ source: pick() }),
      keepalive: true,
    }).catch(() => {});
  })();

  // ---------- Sticky nav, scroll progress & mobile menu ----------
  const nav = document.getElementById('nav');
  const progressBar = document.getElementById('scroll-progress');
  const toggle = document.getElementById('menu-toggle');
  const links = document.getElementById('nav-links');

  const updateScrollUI = () => {
    if (nav) nav.classList.toggle('scrolled', window.scrollY > 18);
    if (progressBar) {
      const docH = document.documentElement.scrollHeight - window.innerHeight;
      const pct = docH > 20 ? Math.min(100, Math.max(0, (window.scrollY / docH) * 100)) : 0;
      progressBar.style.width = `${pct.toFixed(1)}%`;
    }
  };
  updateScrollUI();
  window.addEventListener('scroll', updateScrollUI, { passive: true });

  const setMenu = (open) => {
    if (!links || !toggle) return;
    links.classList.toggle('open', open);
    toggle.setAttribute('aria-expanded', String(open));
  };
  if (toggle && links) {
    toggle.addEventListener('click', () => setMenu(!links.classList.contains('open')));
    links.addEventListener('click', (e) => { if (e.target.closest('a')) setMenu(false); });
  }
  document.addEventListener('keydown', (e) => { if (e.key === 'Escape') setMenu(false); });

  // ---------- Active section highlight in nav ----------
  if (links && 'IntersectionObserver' in window) {
    const navAnchors = [...links.querySelectorAll('a[href^="#"]')];
    const map = new Map();
    for (const a of navAnchors) {
      const target = document.querySelector(a.getAttribute('href'));
      if (target) map.set(target, a);
    }
    if (map.size) {
      const navObs = new IntersectionObserver((entries) => {
        for (const e of entries) {
          if (e.isIntersecting) {
            for (const a of navAnchors) a.classList.remove('active');
            const active = map.get(e.target);
            if (active) active.classList.add('active');
          }
        }
      }, { rootMargin: '-28% 0px -60% 0px' });
      for (const section of map.keys()) navObs.observe(section);
    }
  }

  // ---------- Reveal on scroll ----------
  const revealItems = document.querySelectorAll('.reveal');
  if ('IntersectionObserver' in window && !reduced) {
    const io = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          entry.target.classList.add('in');
          io.unobserve(entry.target);
        }
      }
    }, { rootMargin: '0px 0px -7% 0px', threshold: 0.04 });
    revealItems.forEach((el) => io.observe(el));
  } else {
    revealItems.forEach((el) => el.classList.add('in'));
  }

  // ---------- Pointer spotlight on glass cards ----------
  if (!reduced) {
    document.addEventListener('pointermove', (e) => {
      const card = e.target && e.target.closest ? e.target.closest('.spot-card') : null;
      if (!card) return;
      const r = card.getBoundingClientRect();
      card.style.setProperty('--mx', `${Math.round(e.clientX - r.left)}px`);
      card.style.setProperty('--my', `${Math.round(e.clientY - r.top)}px`);
    }, { passive: true });
  }

  // ---------- Hero live engine scene switcher ----------
  const heroShot = document.getElementById('hero-shot');
  const heroImg = document.getElementById('hero-main-img');
  const heroTag = document.getElementById('hero-scene-tag');
  const heroCap = document.getElementById('hero-scene-caption');
  const sceneTabs = [...document.querySelectorAll('.scene-tab[data-scene-img]')];
  if (heroImg && sceneTabs.length) {
    let sceneIdx = 0;
    let paused = false;
    const setScene = (idx) => {
      sceneIdx = ((idx % sceneTabs.length) + sceneTabs.length) % sceneTabs.length;
      const btn = sceneTabs[sceneIdx];
      for (const b of sceneTabs) b.classList.toggle('active', b === btn);
      const nextSrc = btn.dataset.sceneImg;
      if (heroImg.getAttribute('src') !== nextSrc) {
        if (!reduced) heroImg.classList.add('fading');
        setTimeout(() => {
          heroImg.src = nextSrc;
          heroImg.classList.remove('fading');
        }, reduced ? 0 : 120);
      }
      if (heroTag && btn.dataset.sceneTag) heroTag.textContent = btn.dataset.sceneTag;
      if (heroCap && btn.dataset.sceneCap) heroCap.textContent = btn.dataset.sceneCap;
    };
    sceneTabs.forEach((btn, i) => btn.addEventListener('click', () => {
      paused = true;
      setScene(i);
    }));
    if (heroShot && !reduced) {
      heroShot.addEventListener('pointerenter', () => { paused = true; });
      heroShot.addEventListener('pointerleave', () => { paused = false; });
      setInterval(() => {
        if (!paused && !document.hidden) setScene(sceneIdx + 1);
      }, 6500);
    }
  }

  // ---------- Interactive 18-Realm Atlas ----------
  const explorer = document.getElementById('realms-explorer');
  if (explorer) {
    const tabs = [...explorer.querySelectorAll('.realm-tab[data-realm]')];
    const panels = [...explorer.querySelectorAll('[data-realm-panel]')];
    for (const tab of tabs) {
      tab.addEventListener('click', () => {
        const key = tab.dataset.realm;
        explorer.dataset.activeRealm = key;
        for (const t of tabs) {
          const on = t === tab;
          t.classList.toggle('active', on);
          t.setAttribute('aria-selected', String(on));
        }
        for (const p of panels) {
          const on = p.dataset.realmPanel === key;
          p.classList.toggle('active', on);
          p.hidden = !on;
        }
      });
    }
  }

  // ---------- Controls platform switcher & smart device detection ----------
  const ctrlBox = document.getElementById('controls-box');
  const setCtrlPlatform = (mode) => {
    if (!ctrlBox) return;
    for (const t of ctrlBox.querySelectorAll('.ctrl-tab[data-ctrl-tab]')) {
      const on = t.dataset.ctrlTab === mode;
      t.classList.toggle('active', on);
      t.setAttribute('aria-selected', String(on));
    }
    for (const p of ctrlBox.querySelectorAll('[data-ctrl-panel]')) {
      const on = p.dataset.ctrlPanel === mode;
      p.classList.toggle('active', on);
      p.hidden = !on;
    }
  };
  if (ctrlBox) {
    ctrlBox.querySelectorAll('.ctrl-tab[data-ctrl-tab]').forEach((btn) => {
      btn.addEventListener('click', () => setCtrlPlatform(btn.dataset.ctrlTab));
    });
  }

  const ua = navigator.userAgent || '';
  const isAndroid = /Android/i.test(ua);
  const isWindows = /Windows/i.test(ua);
  const deviceHint = document.getElementById('device-hint');
  if (isAndroid) {
    if (deviceHint) {
      deviceHint.textContent = 'Обнаружен Android — рекомендуем сборку ARM64 APK с сенсорным HUD';
      deviceHint.hidden = false;
    }
    const card = document.querySelector('[data-platform-card="android"]');
    if (card) card.classList.add('recommended-device');
    setCtrlPlatform('android');
  } else if (isWindows) {
    if (deviceHint) {
      deviceHint.textContent = 'Обнаружена Windows — рекомендуем архив Windows x64 (или QR-код для смартфона)';
      deviceHint.hidden = false;
    }
    const card = document.querySelector('[data-platform-card="windows"]');
    if (card) card.classList.add('recommended-device');
    setCtrlPlatform('pc');
  }

  // ---------- Share ----------
  const shareButtons = document.querySelectorAll('[data-share]');
  if (shareButtons.length) {
    const label = (button, text) => {
      const original = button.dataset.label || button.textContent;
      button.dataset.label = original;
      button.textContent = text;
      setTimeout(() => { button.textContent = original; }, 2400);
    };
    shareButtons.forEach((button) => button.addEventListener('click', async () => {
      const url = `${location.origin}/?ref=share`;
      const text = 'LestingCraft: New Era — воксельная песочница, 18 миров, Windows и Android, кооператив до 8 игроков.';
      if (navigator.share) {
        try { await navigator.share({ title: 'LestingCraft: New Era', text, url }); return; } catch { /* cancelled */ }
      }
      try {
        await navigator.clipboard.writeText(`${text}\n${url}`);
        label(button, 'Ссылка скопирована');
      } catch {
        window.prompt('Скопируйте ссылку', url);
      }
    }));
  }

  // ---------- Server status ----------
  const status = document.getElementById('server-status');
  const statusText = status ? status.querySelector('span') : null;
  if (status && statusText) {
    fetch('/api/status', { headers: { Accept: 'application/json' } })
      .then((r) => (r.ok ? r.json() : Promise.reject(new Error(String(r.status)))))
      .then((d) => {
        if (d && d.online === true) {
          status.dataset.state = 'online';
          statusText.textContent = 'Кооператив доступен';
        } else {
          status.dataset.state = 'offline';
          statusText.textContent = 'Кооператив недоступен';
        }
      })
      .catch(() => {
        status.dataset.state = 'unknown';
        statusText.textContent = 'Статус кооператива недоступен';
      });
  }

  // ---------- Open worlds directory (/api/worlds) ----------
  const worlds = document.getElementById('worlds');
  const worldsList = document.getElementById('worlds-list');
  const worldsSummary = document.getElementById('worlds-summary');
  if (worlds && worldsList && worldsSummary) {
    const renderWorlds = (data) => {
      const list = Array.isArray(data && data.worlds) ? data.worlds : [];
      worldsList.textContent = '';
      if (!list.length) {
        worlds.dataset.state = 'empty';
        worldsSummary.textContent = 'Сейчас никто не открыл мир. Откройте свой — и он появится здесь.';
        return;
      }
      worlds.dataset.state = 'ready';
      const players = list.reduce((sum, w) => sum + (Number(w.players) || 0), 0);
      worldsSummary.textContent = `Открыто миров: ${list.length} · игроков в них: ${players}`;
      list.slice(0, 6).forEach((world) => {
        const row = document.createElement('li');
        const name = document.createElement('b');
        name.textContent = world.name || 'Мир';
        const meta = document.createElement('span');
        meta.textContent = `${world.players}/${world.maxPlayers} игроков${world.started ? '' : ' · лобби'}`;
        const copy = document.createElement('button');
        copy.type = 'button';
        copy.className = 'btn ghost tiny';
        copy.textContent = 'Код мира';
        copy.addEventListener('click', async () => {
          try {
            await navigator.clipboard.writeText(world.code);
            copy.textContent = 'Скопировано';
            setTimeout(() => { copy.textContent = 'Код мира'; }, 2000);
          } catch { window.prompt('Код мира', world.code); }
        });
        row.append(name, meta, copy);
        worldsList.append(row);
      });
    };
    fetch('/api/worlds', { headers: { Accept: 'application/json' } })
      .then((r) => (r.ok ? r.json() : Promise.reject(new Error(String(r.status)))))
      .then(renderWorlds)
      .catch(() => {
        worlds.dataset.state = 'error';
        worldsSummary.textContent = 'Список миров сейчас недоступен — сервер не ответил.';
      });
  }

  // ---------- Direct platform downloads (/api/releases -> /releases.json fallback) ----------
  const downloadPanel = document.getElementById('download-builds');
  const downloadStatus = document.getElementById('download-status');
  if (downloadPanel && downloadStatus) {
    const historyWrap = document.getElementById('release-history');
    const historyList = document.getElementById('release-history-list');
    const linksByPlatform = Object.fromEntries(
      [...downloadPanel.querySelectorAll('[data-download]')].map((link) => [link.dataset.download, link])
    );
    const formatSize = (bytes) => {
      const value = Number(bytes);
      if (!Number.isFinite(value) || value <= 0) return '';
      return value >= 1024 * 1024 * 1024
        ? `${(value / (1024 * 1024 * 1024)).toFixed(2)} GB`
        : `${(value / (1024 * 1024)).toFixed(1)} MB`;
    };

    const renderReleaseArchive = (releases) => {
      if (!historyWrap || !historyList) return;
      historyList.textContent = '';
      if (!Array.isArray(releases) || releases.length === 0) {
        historyWrap.hidden = true;
        return;
      }
      for (const rel of releases.slice(0, 8)) {
        const row = document.createElement('div');
        row.className = 'rel-row';
        const info = document.createElement('div');
        const b = document.createElement('b');
        b.textContent = rel.tag || rel.name || 'Релиз';
        const rawDate = rel.date || rel.publishedAt || '';
        const date = rawDate ? ` · ${String(rawDate).slice(0, 10)}` : '';
        const span = document.createElement('span');
        span.className = 'muted';
        span.textContent = date;
        info.append(b, span);

        const rowLinks = document.createElement('div');
        rowLinks.className = 'rel-row-links';
        if (rel.android && rel.android.url) {
          const a = document.createElement('a');
          a.href = rel.android.url;
          a.rel = 'noreferrer';
          a.textContent = `Android APK (${formatSize(rel.android.size) || 'ARM64'})`;
          rowLinks.append(a);
        }
        if (rel.windows && rel.windows.url) {
          const w = document.createElement('a');
          w.href = rel.windows.url;
          w.rel = 'noreferrer';
          w.textContent = `Windows ZIP (${formatSize(rel.windows.size) || 'x64'})`;
          rowLinks.append(w);
        }
        row.append(info, rowLinks);
        historyList.append(row);
      }
      historyWrap.hidden = false;
    };

    const applyReleaseData = (data) => {
      const list = Array.isArray(data && data.releases) ? data.releases : [];
      const release = list.find((item) => item.windows?.url && item.android?.url) || null;
      if (!release) {
        downloadPanel.dataset.state = 'empty';
        downloadStatus.dataset.state = 'empty';
        downloadStatus.textContent = 'Проверенные сборки ещё не опубликованы. Когда выйдет комплект для Windows и Android, загрузка начнётся по одной кнопке.';
        return;
      }
      const labelTitle = release.name || release.tag || '';
      for (const platform of ['windows', 'android']) {
        const link = linksByPlatform[platform];
        const asset = release[platform];
        if (!link || !asset || typeof asset.url !== 'string') continue;
        link.href = asset.url;
        link.removeAttribute('aria-disabled');
        link.removeAttribute('tabindex');
        link.rel = 'noreferrer';
        link.textContent = platform === 'windows' ? 'Скачать для Windows' : 'Скачать APK для Android';
        const meta = downloadPanel.querySelector(`[data-build-meta="${platform}"]`);
        const shaBtn = downloadPanel.querySelector(`[data-copy-sha="${platform}"]`);
        const digest = asset.digest || (asset.sha256 ? `sha256:${asset.sha256}` : '');
        if (meta) {
          const details = [labelTitle, formatSize(asset.size)].filter(Boolean);
          meta.textContent = details.join(' · ');
          if (digest) meta.title = `Контрольная сумма: ${digest}`;
        }
        if (shaBtn && digest) {
          shaBtn.hidden = false;
          shaBtn.title = `Скопировать контрольную сумму: ${digest}`;
          shaBtn.onclick = async () => {
            try {
              await navigator.clipboard.writeText(digest);
              shaBtn.textContent = 'Скопировано';
              setTimeout(() => { shaBtn.textContent = 'SHA-256'; }, 2000);
            } catch {
              window.prompt('Контрольная сумма:', digest);
            }
          };
        }
      }
      renderReleaseArchive(list);
      downloadPanel.dataset.state = 'ready';
      downloadStatus.dataset.state = 'ready';
      const releaseKind = release.prerelease ? 'Предварительная сборка' : 'Стабильная сборка';
      downloadStatus.textContent = `${releaseKind} ${labelTitle}. Нажмите кнопку платформы — загрузка файла начнётся сразу.`;
    };

    fetch('/api/releases', { headers: { Accept: 'application/json' } })
      .then((r) => (r.ok ? r.json() : Promise.reject(new Error(String(r.status)))))
      .then(applyReleaseData)
      .catch(() => {
        fetch('/releases.json', { headers: { Accept: 'application/json' } })
          .then((r) => (r.ok ? r.json() : Promise.reject(new Error(String(r.status)))))
          .then(applyReleaseData)
          .catch(() => {
            downloadPanel.dataset.state = 'error';
            downloadStatus.dataset.state = 'error';
            downloadStatus.textContent = 'Не удалось получить список сборок. Попробуйте обновить страницу позже.';
          });
      });
  }

  // ---------- Lightbox (gallery [data-shot] + boss bestiary [data-boss-shot]) ----------
  const dialog = document.getElementById('lightbox');
  const lbImg = document.getElementById('lb-img');
  const lbCaption = document.getElementById('lb-caption');
  if (dialog && lbImg && lbCaption) {
    const openShot = (src, caption, alt) => {
      lbImg.src = src;
      lbImg.alt = alt || '';
      lbCaption.textContent = caption || '';
      if (typeof dialog.showModal === 'function') dialog.showModal();
    };
    document.querySelectorAll('[data-shot]').forEach((btn) => {
      btn.addEventListener('click', () => {
        const img = btn.querySelector('img');
        openShot(btn.dataset.shot, btn.dataset.caption, img ? img.alt : '');
      });
    });
    document.querySelectorAll('[data-boss-shot]').forEach((btn) => {
      btn.addEventListener('click', () => {
        const img = btn.querySelector('img');
        openShot(btn.dataset.bossShot, btn.dataset.caption, img ? img.alt : '');
      });
    });
    document.getElementById('lb-close')?.addEventListener('click', () => dialog.close());
    dialog.addEventListener('click', (e) => { if (e.target === dialog) dialog.close(); });
    dialog.addEventListener('close', () => { lbImg.removeAttribute('src'); });
  }

  // ---------- Waitlist form ----------
  const form = document.getElementById('waitlist');
  const message = document.getElementById('form-status');
  const submit = form ? form.querySelector('button[type="submit"]') : null;
  const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;
  const say = (text, kind) => { if (!message) return; message.textContent = text; message.dataset.kind = kind; };

  if (form) form.addEventListener('submit', async (e) => {
    e.preventDefault();
    if (form.dataset.busy === 'true') return;
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
    if (submit) submit.textContent = 'Отправляем…';
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
        say(body.message || 'Готово! Напишем, когда выйдет крупное обновление.', 'ok');
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
      if (submit) submit.textContent = 'Записаться';
    }
  });

  // ---------- Voxel landscape background ----------
  const canvas = document.getElementById('voxel-bg');
  if (canvas && canvas.getContext && !reduced && window.innerWidth > 820) {
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
          const hue = 156 - k * 42;
          const sat = 30 + k * 22;
          const light = 17 + k * 22 + hash(c, r) * 3;
          const top = [hue, sat, light];
          const topPts = [[X, Y - z * hz], [X + tw / 2, Y + th / 2 - z * hz], [X, Y + th - z * hz], [X - tw / 2, Y + th / 2 - z * hz]];
          const left = [[X - tw / 2, Y + th / 2 - z * hz], [X, Y + th - z * hz], [X, Y + th], [X - tw / 2, Y + th / 2]];
          const right = [[X, Y + th - z * hz], [X + tw / 2, Y + th / 2 - z * hz], [X + tw / 2, Y + th / 2], [X, Y + th]];
          const warm = k > 0.72 ? 'hsla(38 92% 64% / 0.22)' : null;
          poly(left, shade(top, -10));
          poly(right, shade(top, -5));
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
