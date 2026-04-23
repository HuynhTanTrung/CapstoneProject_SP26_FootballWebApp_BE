const API_BASE = 'http://localhost:5272';
const WEB_BASE = 'http://localhost:5173';

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
      chrome.runtime.sendMessage({ type: 'fetch-image', url }, (res) => {
        if (res?.success) img.src = res.dataUrl;
      });
    });
  } catch (err) {
    console.error('[VN Football]', err);
    removePopup();
  }
}

document.addEventListener('mouseup', (e) => {
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
  // Get title or first h1
  const title = document.querySelector('h1')?.innerText || document.title || '';
  if (!title || title.length < 5) return;

  try {
    const response = await new Promise((resolve, reject) => {
      try {
        chrome.runtime.sendMessage({ type: 'identify', text: title.substring(0, 200) }, (res) => {
          if (chrome.runtime.lastError) reject(chrome.runtime.lastError);
          else resolve(res);
        });
      } catch (e) { reject(e); }
    });

    if (!response?.success) return;
    const foundPlayers = response.playerData?.players?.filter(p => p.found) || [];
    const foundMatch = response.matchData?.success ? response.matchData.match : null;
    if (!foundPlayers.length && !foundMatch) return;

    pageResult = { foundPlayers, foundMatch };

    // Show floating button
    removeFloatBtn();
    floatBtn = document.createElement('div');
    floatBtn.id = 'vn-football-float';
    const count = foundPlayers.length + (foundMatch ? 1 : 0);
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
  } catch (e) { /* silent */ }
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
    chrome.runtime.sendMessage({ type: 'fetch-image', url }, (res) => {
      if (res?.success) img.src = res.dataUrl;
    });
  });
}

// Run auto-detect after page loads
if (document.readyState === 'complete') {
  setTimeout(autoDetectPage, 1500);
} else {
  window.addEventListener('load', () => setTimeout(autoDetectPage, 1500));
}
