const API_BASE = 'http://localhost:5272';
const WEB_BASE = 'http://localhost:5173';

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
      } else if (isLocalhost) {
        chrome.storage.local.remove(['vnfootball_token', 'vnfootball_token_source']);
      }
    } catch { /* extension context invalidated */ }
  }

  pushToken();
  window.addEventListener('auth:login', pushToken);
  window.addEventListener('auth:logout', () => {
    if (!isExtensionValid()) return;
    try {
      if (isLocalhost) chrome.storage.local.remove(['vnfootball_token', 'vnfootball_token_source']);
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

function removePopup() {
  if (host) { host.remove(); host = null; }
}

function showPopup(x, y, html) {
  removePopup();

  // Use Shadow DOM to isolate from page CSS
  host = document.createElement('div');
  host.id = 'vn-football-host';
  host.style.cssText = 'all:initial;position:fixed;z-index:2147483647;left:' +
    Math.max(4, Math.min(x, window.innerWidth - 250)) + 'px;top:' +
    Math.max(4, y) + 'px';

  const shadow = host.attachShadow({ mode: 'open' });
  shadow.innerHTML = `
    <style>
      :host { all: initial; }
      .popup {
        background: #1a1a2e;
        border: 1px solid #2a2a4a;
        border-radius: 10px;
        padding: 6px 8px;
        display: inline-flex;
        flex-direction: column;
        gap: 4px;
        box-shadow: 0 4px 20px rgba(0,0,0,0.5);
        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
        font-size: 12px;
        color: #fff;
        max-width: 230px;
        position: relative;
      }
      .header {
        display: flex;
        align-items: center;
        gap: 5px;
        font-weight: 600;
        color: #e94560;
        font-size: 11px;
        padding-right: 14px;
        line-height: 1.4;
      }
      .card {
        display: flex;
        align-items: center;
        gap: 8px;
        padding: 5px 6px;
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

    if (!foundPlayers.length && !foundMatch) {
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
        <img src="" data-photo="${p.player.photoUrl || ''}"
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
          <div class="meta">${foundMatch.round || ''} · ${foundMatch.matchDate ? new Date(foundMatch.matchDate).toLocaleDateString('vi-VN') : ''}</div>
        </div>
      </div>
    ` : '';

    showPopup(x, yPos, `
      <span class="close">×</span>
      <div class="header">⚽ VN Football</div>
      ${matchCard}
      ${playerCards}
    `);

    // Load player images
    host.shadowRoot.querySelectorAll('img[data-photo]').forEach(img => {
      const url = img.dataset.photo;
      if (!url) return;
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
  // Skip on Google domains
  if (location.hostname.includes('google.com') || location.hostname.includes('gemini.google')) return;
  clearTimeout(debounceTimer);
  debounceTimer = setTimeout(() => {
    const text = window.getSelection()?.toString().trim();
    if (!text || text.length < 3 || text.length > 100 || text === lastText) return;
    if (host && host.contains(e.target)) return;
    lastText = text;
    identifyPlayer(text, e.clientX, e.clientY);
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
    ];
    noiseSelectors.forEach(sel => {
      try { clone.querySelectorAll(sel).forEach(el => el.remove()); } catch {}
    });

    // Try specific article selectors first on the cleaned clone
    const articleSelectors = [
      '.article-body', '.article-content', '.article__body', '.article__content',
      '.post-content', '.entry-content', '.content-detail', '.detail-content',
      '.detail__content', '.news-content', '.story-body',
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

    // Fallback: use all remaining text from cleaned clone
    if (articleBody.length < 200) {
      articleBody = safeText(clone).replace(/\s+/g, ' ').trim();
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

  try {
    console.log('[VN Football] Auto-detecting, text length:', fullText.length, '| preview:', fullText.substring(0, 200));
    console.log('[VN Football] Full text sent:', fullText.substring(0, 500));
    const response = await new Promise((resolve, reject) => {
      try {
        chrome.runtime.sendMessage({ type: 'identify', text: fullText }, (res) => {
          if (chrome.runtime.lastError) reject(chrome.runtime.lastError);
          else resolve(res);
        });
      } catch (e) { reject(e); }
    });

    console.log('[VN Football] Auto-detect response:', response?.success, 
      'players:', response?.playerData?.players?.filter(p=>p.found)?.length,
      'match:', response?.matchData?.success,
      'matchData:', JSON.stringify(response?.matchData)?.substring(0, 200));
    if (!response?.success) return;
    const foundPlayers = response.playerData?.players?.filter(p => p.found) || [];
    const foundMatch = response.matchData?.success ? response.matchData.match : null;
    if (!foundPlayers.length && !foundMatch) {
      // Fallback: try identify-match with just the title
      const titleOnly = document.querySelector('h1')?.innerText?.substring(0, 150) || '';
      if (titleOnly) {
        try {
          if (!chrome?.runtime?.id) return;
          const fallback = await new Promise((resolve) => {
            chrome.runtime.sendMessage({ type: 'identify-match-only', text: titleOnly }, (res) => resolve(res));
          });
          if (fallback?.success && fallback?.match) {
            pageResult = { foundPlayers: [], foundMatch: fallback.match };
            showFloatBtn(1);
            return;
          }
        } catch { /* ignore */ }
      }
      return;
    }

    pageResult = { foundPlayers, foundMatch };

    // Show floating button
    removeFloatBtn();
    const count = foundPlayers.length + (foundMatch ? 1 : 0);
    showFloatBtn(count);
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
  floatBtn.innerHTML = `⚽ ${count} kết quả`;
  floatBtn.addEventListener('click', () => {
    const rect = floatBtn.getBoundingClientRect();
    showResults(rect.left - 10, rect.top - 10, pageResult.foundPlayers, pageResult.foundMatch);
  });
  document.body.appendChild(floatBtn);
}

function showResults(x, y, foundPlayers, foundMatch) {
  const yPos = y - 20 > 200 ? y - 20 : y + 40;

  const playerCards = foundPlayers.map(p => `
    <div class="card" data-url="${p.player.profileUrl}">
      <img src="" data-photo="${p.player.photoUrl || ''}"
           style="width:30px;height:30px;border-radius:50%;object-fit:cover;flex-shrink:0;background:#333" />
      <div class="info">
        <div class="name">${p.player.fullName}</div>
        <div class="meta">${[p.player.teamName, p.player.position].filter(Boolean).join(' · ')}</div>
      </div>
    </div>
  `).join('');

  const score = foundMatch ? (foundMatch.homeGoals != null ? `${foundMatch.homeGoals} - ${foundMatch.awayGoals}` : 'vs') : '';
  const matchCard = foundMatch ? `
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
  ` : '';

  showPopup(x, yPos, `
    <span class="close">×</span>
    <div class="header">⚽ VN Football</div>
    ${matchCard}
    ${playerCards}
  `);

  host.shadowRoot.querySelectorAll('img[data-photo]').forEach(img => {
    const url = img.dataset.photo;
    if (!url) return;
    try {
      chrome.runtime.sendMessage({ type: 'fetch-image', url }, (res) => {
        if (res?.success) img.src = res.dataUrl;
      });
    } catch { /* extension context invalidated */ }
  });
}

// Run auto-detect after page loads — skip on the web app itself
const isWebApp = location.host.includes('localhost:5173') ||
                 location.host.includes('localhost:3000') ||
                 location.host.includes('vnfootballanalytics.vercel.app');

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
