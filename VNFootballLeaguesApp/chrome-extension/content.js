// const API_BASE = 'http://localhost:5272';
const API_BASE = 'https://footballwebappservice-cggdfkhcbsdzfnga.southeastasia-01.azurewebsites.net';
// const WEB_BASE = 'http://localhost:5173';
const WEB_BASE = 'https://vnfootballanalytics.vercel.app';

// ── Auto sync token to extension storage ─────────────────────────────────────
(function syncTokenToExtension() {
  const currentHost = location.host;

  const isLocalhost = currentHost.includes('localhost:5173') || currentHost.includes('localhost:3000');
  const isVercel = currentHost.includes('vnfootballanalytics.vercel.app');

  if (!isLocalhost && !isVercel) return;

  function isExtensionValid() {
    try { return !!(chrome?.runtime?.id); } catch { return false; }
  }

  function pushToken() {
    if (!isExtensionValid()) return;
    try {
      const token = localStorage.getItem('accessToken');
      if (token) {
        if (isLocalhost) {
          chrome.storage.local.set({ vnfootball_token: token, vnfootball_token_source: 'localhost' });
        } else if (isVercel) {
          chrome.storage.local.get(['vnfootball_token_source'], (result) => {
            if (!isExtensionValid()) return;
            if (result.vnfootball_token_source !== 'localhost') {
              chrome.storage.local.set({ vnfootball_token: token, vnfootball_token_source: 'vercel' });
            }
          });
        }
      } else {
        // Remove token on logout regardless of source (localhost or Vercel)
        chrome.storage.local.remove(['vnfootball_token', 'vnfootball_token_source']);
      }
    } catch { /* extension context invalidated */ }
  }

  pushToken();
  window.addEventListener('auth:login', pushToken);
  window.addEventListener('auth:logout', () => {
    if (!isExtensionValid()) return;
    try {
      // Always clear token on logout, regardless of source
      chrome.storage.local.remove(['vnfootball_token', 'vnfootball_token_source']);
    } catch { /* ignore */ }
  });
  window.addEventListener('storage', (e) => {
    if (e.key === 'accessToken') pushToken();
  });
})();
// ─────────────────────────────────────────────────────────────────────────────

let host = null;
let hideTimer = null;
let lastText = '';
let debounceTimer = null;

// ── Helper: build team card HTML ─────────────────────────────────────────────
function buildTeamCard(team) {
  const standingInfo = team.standing
    ? `Hạng ${team.standing.rank} · ${team.standing.points} điểm · ${team.standing.played} trận`
    : '';
  const formDots = (team.standing?.form || '').split('').slice(-5).map(r => {
    const color = r === 'W' ? '#4caf50' : r === 'L' ? '#e94560' : '#aaa';
    return `<span style="display:inline-block;width:8px;height:8px;border-radius:50%;background:${color};margin:0 1px"></span>`;
  }).join('');

  // Use backend proxy URL (has Cloudinary cache + proper headers)
  const logoSrc = team.logoProxyUrl ? `${API_BASE}${team.logoProxyUrl}` : '';
  const initials = (team.teamName || '?').charAt(0).toUpperCase();
  const logoHtml = logoSrc
    ? `<img src="${logoSrc}"
           style="width:32px;height:32px;object-fit:contain;flex-shrink:0;border-radius:4px;background:#0f3460"
           onerror="this.style.display='none';this.nextElementSibling.style.display='flex'" />
       <div style="display:none;width:32px;height:32px;border-radius:4px;background:#e94560;color:#fff;font-weight:700;font-size:14px;align-items:center;justify-content:center;flex-shrink:0">${initials}</div>`
    : `<div style="display:flex;width:32px;height:32px;border-radius:4px;background:#e94560;color:#fff;font-weight:700;font-size:14px;align-items:center;justify-content:center;flex-shrink:0">${initials}</div>`;

  return `
    <div class="card team-card" data-url="${team.profileUrl}">
      ${logoHtml}
      <div class="info">
        <div class="name">${team.teamName}</div>
        <div class="meta">${team.stadiumCity || ''}${standingInfo ? ' · ' + standingInfo : ''}</div>
        ${formDots ? `<div style="margin-top:3px">${formDots}</div>` : ''}
      </div>
    </div>
  `;
}
// ─────────────────────────────────────────────────────────────────────────────

function removePopup() {
  if (host) { host.remove(); host = null; }
}

function showPopup(x, y, html) {
  removePopup();

  // Use Shadow DOM to isolate from page CSS
  host = document.createElement('div');
  host.id = 'vn-football-host';

  // Smart positioning: start at click point, adjust after render
  const topPos = Math.max(4, y);

  host.style.cssText = 'all:initial;position:fixed;z-index:2147483647;left:' +
    Math.max(4, Math.min(x, window.innerWidth - 270)) + 'px;top:' +
    topPos + 'px';

  const shadow = host.attachShadow({ mode: 'open' });
  shadow.innerHTML = `
    <style>
      :host { all: initial; }
      .popup {
        background: #1a1a2e;
        border: 1px solid #2a2a4a;
        border-radius: 10px;
        padding: 6px 8px;
        display: flex;
        flex-direction: column;
        gap: 4px;
        box-shadow: 0 4px 20px rgba(0,0,0,0.5);
        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
        font-size: 12px;
        color: #fff;
        width: 260px;
        max-height: 360px;
        overflow-y: auto;
        overflow-x: hidden;
        position: relative;
        scrollbar-width: thin;
        scrollbar-color: #3a3a5a transparent;
        box-sizing: border-box;
      }
      .popup::-webkit-scrollbar { width: 4px; }
      .popup::-webkit-scrollbar-thumb { background: #3a3a5a; border-radius: 4px; }
      .header {
        display: flex;
        align-items: center;
        gap: 5px;
        font-weight: 600;
        color: #e94560;
        font-size: 11px;
        padding-right: 14px;
        line-height: 1.4;
        position: sticky;
        top: 0;
        background: #1a1a2e;
        z-index: 1;
        padding-bottom: 4px;
      }
      .card {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 5px 6px;
        flex-shrink: 0;
        background: #16213e;
        border-radius: 7px;
        cursor: pointer;
        line-height: 1.3;
      }
      .card:hover { background: #0f3460; }
      .card img {
        width: 30px;
        height: 30px;
        border-radius: 50%;
        object-fit: cover;
        flex-shrink: 0;
        background: #333;
      }
      .name { font-weight: 600; font-size: 12px; color: #fff; white-space: nowrap; }
      .meta { color: #aaa; font-size: 10px; white-space: nowrap; }
      .info { display: flex; flex-direction: column; gap: 1px; }
      .close {
        position: absolute; top: 4px; right: 6px;
        cursor: pointer; color: #666; font-size: 13px; line-height: 1;
        background: none; border: none; padding: 0;
      }
      .close:hover { color: #fff; }
      .loading { color: #aaa; font-size: 11px; padding: 2px 0; }
      .notfound { color: #aaa; font-size: 11px; }
      .match-card { flex-direction: column; align-items: flex-start; gap: 2px; }
      .match-info { width: 100%; }
      .match-teams { display: flex; align-items: center; justify-content: space-between; gap: 6px; font-size: 11px; }
      .match-teams .team { color: #fff; font-weight: 600; flex: 1; }
      .match-teams .team:last-child { text-align: right; }
      .match-teams .score { color: #e94560; font-weight: 700; font-size: 13px; white-space: nowrap; }
      .team-card { display: flex; align-items: center; gap: 8px; }
      .team-standing { font-size: 10px; color: #aaa; margin-top: 2px; }
    </style>
    <div class="popup">${html}</div>
  `;

  shadow.querySelector('.close')?.addEventListener('click', removePopup);
  shadow.querySelectorAll('.card').forEach(card => {
    card.addEventListener('click', () => {
      window.open(WEB_BASE + card.dataset.url, '_blank');
      removePopup();
    });
  });

  document.body.appendChild(host);
}

async function identifyPlayer(text, x, y) {
  // Calculate y position - show above if near bottom
  const popupH = 120;
  const yPos = y + popupH > window.innerHeight ? y - popupH - 10 : y + 10;

  showPopup(x, yPos, `
    <span class="close">×</span>
    <div class="header">⚽ VN Football</div>
    <div class="loading">Đang nhận diện...</div>
  `);

  try {
    const response = await new Promise((resolve, reject) => {
      try {
        chrome.runtime.sendMessage({ type: 'identify', text }, (res) => {
          if (chrome.runtime.lastError) {
            // Extension context invalidated - reload will fix
            reject(new Error(chrome.runtime.lastError.message));
          } else resolve(res);
        });
      } catch (e) { reject(e); }
    });

    if (!response?.success) throw new Error(response?.error || 'Failed');

    const foundPlayers = response.playerData?.players?.filter(p => p.found) || [];
    const foundMatch = response.matchData?.success ? response.matchData.match : null;
    const foundTeam = response.teamData?.success ? response.teamData.team : null;

    if (!foundPlayers.length && !foundMatch && !foundTeam) {
      showPopup(x, yPos, `
        <span class="close">×</span>
        <div class="header">⚽ VN Football</div>
        <div class="notfound">Không tìm thấy "${text}"</div>
      `);
      hideTimer = setTimeout(removePopup, 2500);
      return;
    }

    const playerCards = foundPlayers.map(p => `
      <div class="card" data-url="${p.player.profileUrl}">
        <img src="" data-photo="${p.player.photoProxyUrl ? API_BASE + p.player.photoProxyUrl : (p.player.photoUrl || '')}"
             style="width:30px;height:30px;border-radius:50%;object-fit:cover;flex-shrink:0;background:#333" />
        <div class="info">
          <div class="name">${p.player.fullName}</div>
          <div class="meta">${[p.player.teamName, p.player.position].filter(Boolean).join(' · ')}</div>
        </div>
      </div>
    `).join('');

    const score = foundMatch
      ? (foundMatch.homeGoals != null ? `${foundMatch.homeGoals} - ${foundMatch.awayGoals}` : 'vs')
      : '';
    const matchCard = foundMatch ? `
      <div class="card match-card" data-url="/matches/${foundMatch.apiFixtureId || foundMatch.matchId}">
        <div class="match-info">
          <div class="match-teams">
            <span class="team">${foundMatch.homeTeam?.teamName || ''}</span>
            <span class="score">${score}</span>
            <span class="team">${foundMatch.awayTeam?.teamName || ''}</span>
          </div>
          <div class="meta">${foundMatch.round ? 'Vòng ' + foundMatch.round : ''} · ${foundMatch.matchDate ? new Date(foundMatch.matchDate).toLocaleDateString('vi-VN') : ''}</div>
        </div>
      </div>
    ` : '';

    // Team card — only show if no match found (avoid duplicate info)
    const teamCard = (foundTeam && !foundMatch) ? buildTeamCard(foundTeam) : '';

    showPopup(x, yPos, `
      <span class="close">×</span>
      <div class="header">⚽ VN Football</div>
      ${matchCard}
      ${teamCard}
      ${playerCards}
    `);

    // Load player images
    host.shadowRoot.querySelectorAll('img[data-photo]').forEach(img => {
      const url = img.dataset.photo;
      if (!url) return;
      // If it's a proxy URL from our backend, set src directly (no CORS issue)
      if (url.startsWith(API_BASE)) {
        img.src = url;
        img.onerror = () => { img.style.display = 'none'; };
        return;
      }
      // Fallback: fetch external URLs via background script
      try {
        chrome.runtime.sendMessage({ type: 'fetch-image', url }, (res) => {
          if (res?.success) img.src = res.dataUrl;
        });
      } catch { /* extension context invalidated */ }
    });
  } catch (err) {
    console.error('[VN Football]', err);
    removePopup();
  }
}

document.addEventListener('mouseup', (e) => {
  // Skip if extension context is invalidated
  try { if (!chrome?.runtime?.id) return; } catch { return; }
  // Skip on Google domains
  if (location.hostname.includes('google.com') || location.hostname.includes('gemini.google')) return;
  clearTimeout(debounceTimer);
  debounceTimer = setTimeout(() => {
    // Check if text selection feature is enabled
    chrome.storage.local.get(['vnfootball_selection_enabled', 'autodetect_enabled'], (result) => {
      const enabled = result.autodetect_enabled !== false && result.vnfootball_selection_enabled !== false;
      if (!enabled) return;
      const text = window.getSelection()?.toString().trim();
      if (!text || text.length < 3 || text.length > 100 || text === lastText) return;
      if (host && host.contains(e.target)) return;
      lastText = text;
      identifyPlayer(text, e.clientX, e.clientY);
    });
  }, 300);
});

document.addEventListener('mousedown', (e) => {
  if (host && !host.contains(e.target)) {
    hideTimer = setTimeout(removePopup, 150);
  }
});

// ===== Auto-detect from page title/heading =====
let floatBtn = null;
let pageResult = null;

function removeFloatBtn() {
  if (floatBtn) { floatBtn.remove(); floatBtn = null; }
}

async function autoDetectPage() {
  // Guard: extension context may be invalidated after reload
  try { if (!chrome?.runtime?.id) return; } catch { return; }

  // Check if auto-detect is enabled
  const { autodetect_enabled } = await new Promise(resolve =>
    chrome.storage.local.get(['autodetect_enabled'], resolve)
  );
  if (autodetect_enabled === false) return;

  console.log('[VN Football] autoDetectPage called');

  const title = document.querySelector('h1')?.innerText?.trim() || document.title || '';

  // Helper to safely get text from an element
  function safeText(el) {
    try { return el?.innerText?.trim() || el?.textContent?.trim() || ''; } catch { return ''; }
  }

  // Clone body and strip all noise elements to get clean article text
  let articleBody = '';
  try {
    const clone = document.body.cloneNode(true);
    const noiseSelectors = [
      'script', 'style', 'nav', 'header', 'footer', 'aside',
      '[class*="sidebar"]', '[class*="related"]', '[class*="recommend"]',
      '[class*="most-read"]', '[class*="tin-doc"]', '[class*="box-"]',
      '[class*="widget"]', '[class*="ads"]', '[class*="advertisement"]',
      '[class*="social"]', '[class*="share"]', '[class*="comment"]',
      '[class*="tag"]', '[class*="breadcrumb"]', '[class*="menu"]',
      '[class*="navigation"]', '[class*="footer"]', '[class*="header"]',
      '[class*="banner"]', '[class*="popup"]',
      // Vietnamese news site specific
      '[class*="doc-tiep"]', '[class*="read-more"]', '[class*="tin-lien-quan"]',
      '[class*="related-news"]', '[class*="news-related"]', '[class*="other-news"]',
      '[class*="right-col"]', '[class*="right-sidebar"]', '[class*="col-right"]',
    ];
    noiseSelectors.forEach(sel => {
      try { clone.querySelectorAll(sel).forEach(el => el.remove()); } catch {}
    });

    // Try specific article selectors first on the cleaned clone
    const articleSelectors = [
      // baodanang.vn specific
      '.detail-content', '.article-detail', '.content-detail',
      // Common
      '.article-body', '.article-content', '.article__body', '.article__content',
      '.post-content', '.entry-content', '.detail__content', '.news-content', '.story-body',
      'article', '[itemprop="articleBody"]',
      '[class*="article-body"]', '[class*="article-content"]',
      '[class*="content-detail"]', '[class*="detail-content"]',
    ];

    for (const sel of articleSelectors) {
      try {
        const el = clone.querySelector(sel);
        const t = safeText(el);
        if (t.length > 200) { articleBody = t; break; }
      } catch {}
    }

    // Fallback: find the container with the most <p> tags (likely the article body)
    if (articleBody.length < 200) {
      // Find all block-level containers, pick the one with most paragraph text
      const candidates = Array.from(clone.querySelectorAll('div, section, main'))
        .map(el => {
          const paras = Array.from(el.querySelectorAll('p'))
            .map(p => safeText(p))
            .filter(t => t.length > 40);
          return { el, text: paras.join(' '), count: paras.length };
        })
        .filter(c => c.count >= 3 && c.text.length > 200)
        .sort((a, b) => b.text.length - a.text.length);

      if (candidates.length > 0) {
        articleBody = candidates[0].text;
      }
    }

    // Last resort: first 15 paragraphs from cleaned clone
    if (articleBody.length < 100) {
      articleBody = Array.from(clone.querySelectorAll('p'))
        .map(p => safeText(p))
        .filter(t => t.length > 40)
        .slice(0, 15)
        .join(' ');
    }
  } catch (e) {
    // If clone approach fails, fall back to direct paragraph extraction
    articleBody = Array.from(document.querySelectorAll('p'))
      .map(p => safeText(p))
      .filter(t => t.length > 30)
      .slice(0, 30)
      .join(' ');
  }

  const fullText = [title, articleBody].filter(Boolean).join('\n').substring(0, 2000);
  if (!fullText || fullText.length < 50) return;

  // Lead text = title + first 2 sentences (most reliable for match detection)
  const sentences = articleBody.split(/[.!?]/).filter(s => s.trim().length > 10);
  const leadText = [title, sentences.slice(0, 2).join('. ')].filter(Boolean).join('\n').substring(0, 400);

  try {
    console.log('[VN Football] Auto-detecting, text length:', fullText.length, '| lead:', leadText.substring(0, 100));

    const response = await new Promise((resolve, reject) => {
      try {
        chrome.runtime.sendMessage({
          type: 'identify-article',
          leadText,
          fullText,
        }, (res) => {
          if (chrome.runtime.lastError) {
            // Log message string, not the object itself
            console.warn('[VN Football] sendMessage error:', chrome.runtime.lastError.message);
            resolve(null); // treat as no result, don't reject
          } else {
            resolve(res);
          }
        });
      } catch (e) { reject(e); }
    });

    if (!response?.success) return;

    const mode = response.mode; // 'match' or 'entities'
    const foundMatch = response.matchData?.success ? response.matchData.match : null;
    const foundPlayers = response.playerData?.players?.filter(p => p.found) || [];
    const foundTeam = response.teamData?.success ? response.teamData.team : null;

    console.log('[VN Football] mode:', mode, '| match:', foundMatch?.matchId, '| players:', foundPlayers.length, '| team:', foundTeam?.teamName);

    if (mode === 'match' && foundMatch) {
      pageResult = { mode: 'match', foundPlayers: [], foundMatch, foundTeam: null };
      removeFloatBtn();
      showFloatBtn(1);
    } else if (mode === 'entities' && (foundPlayers.length || foundTeam)) {
      pageResult = { mode: 'entities', foundPlayers, foundMatch: null, foundTeam };
      removeFloatBtn();
      const count = foundPlayers.length + (foundTeam ? 1 : 0);
      showFloatBtn(count);
    }
  } catch (e) { console.error('[VN Football] autoDetect error:', e); }
}

function showFloatBtn(count) {
  removeFloatBtn();
  floatBtn = document.createElement('div');
  floatBtn.id = 'vn-football-float';
  floatBtn.style.cssText = `
    all:initial;position:fixed;bottom:80px;right:16px;z-index:2147483647;
    background:#e94560;color:#fff;border-radius:50px;padding:8px 14px;
    font-family:-apple-system,sans-serif;font-size:12px;font-weight:600;
    cursor:pointer;box-shadow:0 4px 16px rgba(233,69,96,0.4);
    display:flex;align-items:center;gap:6px;
  `;
  const label = pageResult?.mode === 'match' ? '⚽ Xem trận đấu' : `⚽ ${count} kết quả`;
  floatBtn.innerHTML = label;
  floatBtn.addEventListener('click', () => {
    const rect = floatBtn.getBoundingClientRect();
    showResults(rect.left - 10, rect.top - 10, pageResult.foundPlayers, pageResult.foundMatch, pageResult.foundTeam, pageResult.mode);
  });
  document.body.appendChild(floatBtn);
}

function showResults(x, y, foundPlayers, foundMatch, foundTeam, mode) {
  const yPos = y - 20 > 200 ? y - 20 : y + 40;

  let html = '';

  if (mode === 'match' && foundMatch) {
    // Match mode: show match card prominently
    const score = foundMatch.homeGoals != null ? `${foundMatch.homeGoals} - ${foundMatch.awayGoals}` : 'vs';
    html = `
      <div class="card match-card" data-url="${foundMatch.profileUrl}">
        <div class="match-info">
          <div class="match-teams">
            <span class="team">${foundMatch.homeTeam?.teamName || ''}</span>
            <span class="score">${score}</span>
            <span class="team">${foundMatch.awayTeam?.teamName || ''}</span>
          </div>
          <div class="meta">${foundMatch.round ? 'Vòng ' + foundMatch.round : ''} · ${foundMatch.matchDate ? new Date(foundMatch.matchDate).toLocaleDateString('vi-VN') : ''}</div>
        </div>
      </div>
    `;
  } else {
    // Entities mode: show team + players
    if (foundTeam) html += buildTeamCard(foundTeam);
    html += foundPlayers.map(p => `
      <div class="card" data-url="${p.player.profileUrl}">
        <img src="" data-photo="${p.player.photoProxyUrl ? API_BASE + p.player.photoProxyUrl : (p.player.photoUrl || '')}"
             style="width:30px;height:30px;border-radius:50%;object-fit:cover;flex-shrink:0;background:#333" />
        <div class="info">
          <div class="name">${p.player.fullName}</div>
          <div class="meta">${[p.player.teamName, p.player.position].filter(Boolean).join(' · ')}</div>
        </div>
      </div>
    `).join('');
  }

  showPopup(x, yPos, `
    <span class="close">×</span>
    <div class="header">⚽ VN Football</div>
    ${html}
  `);

  // Adjust position after render based on actual popup height
  requestAnimationFrame(() => {
    if (!host) return;
    const popupEl = host.shadowRoot?.querySelector('.popup');
    if (!popupEl) return;
    const actualH = popupEl.offsetHeight;
    const currentTop = parseInt(host.style.top);
    // If popup goes below viewport, move it up
    if (currentTop + actualH > window.innerHeight - 10) {
      host.style.top = Math.max(4, window.innerHeight - actualH - 10) + 'px';
    }
  });

  host.shadowRoot.querySelectorAll('img[data-photo]').forEach(img => {
    const url = img.dataset.photo;
    if (!url) return;
    // If it's a proxy URL from our backend, set src directly (no CORS issue)
    if (url.startsWith(API_BASE)) {
      img.src = url;
      img.onerror = () => { img.style.display = 'none'; };
      return;
    }
    console.log('[VN Football] Loading image:', url.substring(0, 60));
    try {
      chrome.runtime.sendMessage({ type: 'fetch-image', url }, (res) => {
        if (res?.success) img.src = res.dataUrl;
        else console.log('[VN Football] Image load failed for:', url.substring(0, 60));
      });
    } catch { /* extension context invalidated */ }
  });
}

// Run auto-detect after page loads — skip on the web app itself
const isWebApp = location.host.includes('localhost:5173') ||
                 location.host.includes('localhost:3000') ||
                 location.host.includes('vnfootballanalytics.vercel.app');

// Listen for toggle changes from popup
chrome.runtime.onMessage.addListener((msg) => {
  if (msg.type === 'set-autodetect') {
    if (!msg.enabled) {
      removeFloatBtn();
      removePopup();
    }
  }
});

if (!isWebApp) {
  async function scheduleDetect(attempt = 1) {
    const delays = [1500, 3000, 5000];
    if (attempt > delays.length) return;

    setTimeout(async () => {
      const textLen = document.body?.innerText?.length ?? 0;
      console.log('[VN Football] Page text length at attempt', attempt, ':', textLen);

      await autoDetectPage();

      // Always retry attempt 2 (page may still be JS-rendering at 1.5s)
      // For attempt 3+, only retry if floatBtn still not shown
      if (attempt === 1) {
        removeFloatBtn(); // discard attempt-1 result, wait for more content
        scheduleDetect(attempt + 1);
      } else if (!floatBtn) {
        scheduleDetect(attempt + 1);
      }
    }, delays[attempt - 1]);
  }

  if (document.readyState === 'complete') {
    scheduleDetect();
  } else {
    window.addEventListener('load', () => scheduleDetect());
  }
}
