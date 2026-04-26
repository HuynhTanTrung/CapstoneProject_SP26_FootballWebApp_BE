// const API_BASE = 'http://localhost:5272/api';
const API_BASE = 'https://footballwebappservice-cggdfkhcbsdzfnga.southeastasia-01.azurewebsites.net/api';

// Web app origins to look for token
const WEB_APP_ORIGINS = [
  '*://localhost:5173/*',
  '*://localhost:3000/*',
  '*://vnfootballanalytics.vercel.app/*',
];

// Vietnamese football keywords for client-side pre-check
const VN_FOOTBALL_KEYWORDS = [
  'v-league', 'v.league', 'vleague', 'hạng nhất', 'cúp quốc gia', 'vietnam cup',
  'bóng đá việt nam', 'bđvn', 'vpf', 'vff',
  'công an hà nội', 'cahn', 'hà nội fc', 'hoàng anh gia lai', 'hagl',
  'thép xanh nam định', 'shb đà nẵng', 'becamex bình dương',
  'sông lam nghệ an', 'slna', 'đông á thanh hóa', 'tp.hcm fc', 'viettel fc',
  'hải phòng fc', 'bình định fc', 'hà tĩnh fc', 'long an fc', 'khánh hòa fc',
  'quảng nam fc', 'pvf', 'bongdaplus', 'bongda.com.vn',
];

const FOREIGN_LEAGUE_KEYWORDS = [
  'premier league', 'la liga', 'bundesliga', 'serie a', 'ligue 1',
  'champions league', 'europa league', 'world cup', 'asian cup',
  'manchester city', 'liverpool', 'real madrid', 'barcelona', 'bayern munich',
  'psg', 'juventus', 'chelsea', 'arsenal', 'inter milan', 'ac milan',
];



// ── Helpers ──────────────────────────────────────────────────────────────────

function show(id) {
  document.getElementById(id)?.classList.remove('hidden');
}

function hide(id) {
  document.getElementById(id)?.classList.add('hidden');
}

function showSection(id) {
  ['section-auth', 'section-premium', 'section-no-credits',
   'section-not-football', 'section-main'].forEach(s => hide(s));
  show(id);
}

function getToken() {
  return new Promise(resolve => {
    chrome.storage.local.get(['vnfootball_token'], result => {
      resolve(result.vnfootball_token || null);
    });
  });
}

function detectLeagueFromPage(title, url, content) {
  const text = (title + ' ' + url + ' ' + content).toLowerCase();
  const hasVN = VN_FOOTBALL_KEYWORDS.some(k => text.includes(k));
  const hasForeign = FOREIGN_LEAGUE_KEYWORDS.some(k => text.includes(k));

  if (hasVN) return 'vn';
  if (hasForeign && !hasVN) return 'foreign';
  return 'unknown'; // let AI decide
}

function renderMarkdown(text) {
  // Minimal markdown → HTML (no external lib needed)
  return text
    .replace(/^## (.+)$/gm, '<h2>$1</h2>')
    .replace(/^### (.+)$/gm, '<h3>$1</h3>')
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/\*(.+?)\*/g, '<em>$1</em>')
    .replace(/^- (.+)$/gm, '<li>$1</li>')
    .replace(/(<li>.*<\/li>\n?)+/g, m => `<ul>${m}</ul>`)
    .replace(/\n\n/g, '</p><p>')
    .replace(/^(?!<[hul])/gm, '')
    .trim();
}

function getBadgeClass(league) {
  if (!league) return 'unknown';
  if (league.includes('1')) return 'vleague1';
  if (league.includes('2')) return 'vleague2';
  if (league.toLowerCase().includes('cup')) return 'cup';
  return 'unknown';
}

function updateCreditsBadge(remaining) {
  const el = document.getElementById('credits-badge');
  if (!el) return;
  el.textContent = `${remaining} lượt còn lại`;
  el.style.display = 'inline-block';
  el.style.background = remaining === 0 ? '#ffebee' : '#e3f2fd';
  el.style.color = remaining === 0 ? '#c62828' : '#1565c0';
}

// ── Main ─────────────────────────────────────────────────────────────────────

async function init() {
  const token = await getToken();
  console.log('[VNFootball] token from storage:', token ? token.substring(0, 20) + '...' : 'null');

  if (!token) {
    // Try one more time: directly inject into active tab via scripting
    try {
      const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
      if (tab?.url && (tab.url.includes('localhost:5173') || tab.url.includes('localhost:3000') || tab.url.includes('vnfootballanalytics'))) {
        const results = await chrome.scripting.executeScript({
          target: { tabId: tab.id },
          func: () => localStorage.getItem('accessToken'),
        });
        const directToken = results?.[0]?.result;
        console.log('[VNFootball] direct inject result:', directToken ? 'found' : 'null');
        if (directToken) {
          await chrome.storage.local.set({ vnfootball_token: directToken });
          initWithToken(directToken);
          return;
        }
      }
    } catch (e) {
      console.log('[VNFootball] scripting inject failed:', e.message);
    }

    showSection('section-auth');
    document.getElementById('btn-retry')?.addEventListener('click', async () => {
      await init();
    });
    return;
  }

  initWithToken(token);
}

async function initWithToken(token) {
  // Get current tab info
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });

  // Clean up old tab-ID-based cache keys (migration from old format)
  chrome.storage.local.get(null, items => {
    const oldKeys = Object.keys(items).filter(k => /^result_\d+$/.test(k));
    if (oldKeys.length > 0) chrome.storage.local.remove(oldKeys);
  });

  // Inject content script to extract article content
  let articleData = null;
  try {
    const results = await chrome.scripting.executeScript({
      target: { tabId: tab.id },
      func: extractArticleContent,
    });
    articleData = results?.[0]?.result;
  } catch {
    // Can't inject (chrome:// pages, etc.)
  }

  const title = articleData?.title || tab.title || '';
  const url = tab.url || '';
  const content = articleData?.content || '';
  // Cache by URL (not tab ID) so navigating to a new article always shows fresh state
  const tabKey = `result_url_${btoa(url).replace(/[^a-zA-Z0-9]/g, '').slice(0, 80)}`;

  console.log('[VNFootball] Extracted content preview:', content.slice(0, 300));
  console.log('[VNFootball] URL:', url);

  // Client-side pre-check
  const detection = detectLeagueFromPage(title, url, content);

  if (detection === 'foreign') {
    showSection('section-not-football');
    return;
  }

  // Validate token expiry
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    if (payload.exp && payload.exp * 1000 < Date.now()) {
      chrome.storage.local.remove('vnfootball_token');
      showSection('section-auth');
      return;
    }
  } catch {
    chrome.storage.local.remove('vnfootball_token');
    showSection('section-auth');
    return;
  }

  // Show main section
  showSection('section-main');

  // Show user info from token
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    const name = payload.unique_name || payload.name || payload.email || 'Người dùng';
    document.getElementById('user-name').textContent = name;
  } catch { /* ignore */ }

  // Fetch and show credits from server
  try {
    const res = await fetch(`${API_BASE}/article-analysis/check-access`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    console.log('[VNFootball] check-access status:', res.status);
    if (res.ok) {
      const access = await res.json();
      console.log('[VNFootball] check-access response:', access);
      updateCreditsBadge(access.creditsRemaining ?? 0);
    } else {
      const body = await res.text();
      console.warn('[VNFootball] check-access failed:', res.status, body);
      updateCreditsBadge(0);
    }
  } catch (e) {
    console.error('[VNFootball] check-access error:', e);
    updateCreditsBadge(0);
  }

  // Show page badge
  if (detection === 'vn' || content.length > 200) {
    const badge = document.getElementById('page-badge');
    badge.classList.remove('hidden');
    badge.textContent = '⚽ Bài viết bóng đá Việt Nam';
    badge.className = `badge ${detection === 'vn' ? 'vleague1' : 'unknown'}`;
  }

  const btnAnalyze = document.getElementById('btn-analyze');

  // ── Restore previous state for this tab ──────────────────────────────────
  const stored = await new Promise(r => chrome.storage.local.get([tabKey, 'analyzing_tab', 'analyzing_start'], r));

  if (stored.analyzing_tab === tab.id) {
    // Was analyzing when popup closed — show "still processing" state
    show('loading');
    btnAnalyze.disabled = true;

    // Calculate elapsed time from when analysis started
    const startTime = stored.analyzing_start || Date.now();
    const alreadyElapsed = Math.floor((Date.now() - startTime) / 1000);

    const loadingSpan = document.querySelector('.loading span');
    loadingSpan.textContent = 'Đang phân tích... (vui lòng chờ)';

    // Restore timer display
    const timerSpan = document.createElement('span');
    timerSpan.style.cssText = 'margin-left:6px;font-size:10px;color:#aaa;';
    document.getElementById('loading').appendChild(timerSpan);

    let elapsed = alreadyElapsed;
    timerSpan.textContent = `${elapsed}s`;
    if (elapsed >= 15) timerSpan.style.color = '#e57373';

    const timerInterval = setInterval(() => {
      elapsed++;
      timerSpan.textContent = `${elapsed}s`;
      if (elapsed >= 15) timerSpan.style.color = '#e57373';
    }, 1000);

    // Add cancel button
    const cancelBtn = document.createElement('button');
    cancelBtn.textContent = '✕ Hủy';
    cancelBtn.style.cssText = 'margin-left:8px;background:none;border:1px solid #ccc;border-radius:6px;padding:2px 8px;cursor:pointer;font-size:11px;color:#666;';
    document.getElementById('loading').appendChild(cancelBtn);
    cancelBtn.addEventListener('click', async () => {
      clearInterval(timerInterval);
      await chrome.storage.local.remove(['analyzing_tab', 'analyzing_start', tabKey]);
      cancelBtn.remove();
      timerSpan.remove();
      hide('loading');
      btnAnalyze.disabled = content.length <= 100;
      btnAnalyze.innerHTML = '<span class="btn-icon">🤖</span> Phân tích bài viết bằng AI';
      btnAnalyze.onclick = () => analyzeArticle(token, { title, url, content }, tabKey);
    });

    // Poll storage every second until result arrives
    const poll = setInterval(async () => {
      const check = await new Promise(r => chrome.storage.local.get([tabKey, 'analyzing_tab'], r));
      if (check.analyzing_tab !== tab.id) {
        clearInterval(poll);
        clearInterval(timerInterval);
        cancelBtn.remove();
        timerSpan.remove();
        hide('loading');
        if (check[tabKey]) {
          displayResult(check[tabKey]);
        }
        btnAnalyze.disabled = content.length <= 100;
      }
    }, 1000);
    return;
  }

  if (stored[tabKey]) {
    // Has cached result for this tab — show it immediately
    displayResult(stored[tabKey]);
    btnAnalyze.disabled = content.length <= 100;
    btnAnalyze.innerHTML = '<span class="btn-icon">🔄</span> Phân tích lại';
    btnAnalyze.onclick = () => {
      chrome.storage.local.remove(tabKey);
      hide('result-container');
      analyzeArticle(token, { title, url, content }, tabKey);
    };
    document.getElementById('btn-copy').onclick = copyResult;
    return;
  }
  // ─────────────────────────────────────────────────────────────────────────

  // Enable analyze button if we have content
  if (content.length > 100) {
    btnAnalyze.disabled = false;
  } else {
    btnAnalyze.disabled = true;
    btnAnalyze.title = 'Không đọc được nội dung trang này';
  }

  btnAnalyze.onclick = () => analyzeArticle(token, { title, url, content }, tabKey);
  document.getElementById('btn-copy').onclick = copyResult;
}

function displayResult(data) {
  const leagueEl = document.getElementById('result-league');
  leagueEl.textContent = data.detectedLeague || 'Bóng đá Việt Nam';
  const contentEl = document.getElementById('result-content');
  const cleaned = (data.analysis || '').replace(/\[GIẢI ĐẤU:[^\]]+\]\s*/g, '');
  contentEl.innerHTML = renderMarkdown(cleaned);

  // Render entity links — format: "Name|ID"
  const entities = data.entities;
  if (entities && (entities.players?.length || entities.teams?.length)) {
    const linksDiv = document.createElement('div');
    linksDiv.style.cssText = 'margin-top:10px;padding-top:8px;border-top:1px solid #eee;';
    linksDiv.innerHTML = '<div style="font-size:11px;font-weight:600;color:#555;margin-bottom:6px;">🔗 Khám phá thêm trên VN Football</div>';

    const WEB = 'http://localhost:5173';
    const chips = document.createElement('div');
    chips.style.cssText = 'display:flex;flex-wrap:wrap;gap:5px;';

    (entities.players || []).forEach(entry => {
      const [name, id] = entry.split('|');
      if (!id) return;
      const a = document.createElement('a');
      a.href = `${WEB}/players/${id}`;
      a.target = '_blank';
      a.textContent = `👤 ${name}`;
      a.style.cssText = 'font-size:11px;padding:3px 8px;background:#e3f2fd;color:#1565c0;border-radius:12px;text-decoration:none;white-space:nowrap;';
      a.onmouseover = () => a.style.background = '#bbdefb';
      a.onmouseout = () => a.style.background = '#e3f2fd';
      chips.appendChild(a);
    });

    (entities.teams || []).forEach(entry => {
      const [name, id] = entry.split('|');
      if (!id) return;
      const a = document.createElement('a');
      a.href = `${WEB}/teams/${id}`;
      a.target = '_blank';
      a.textContent = `🏟️ ${name}`;
      a.style.cssText = 'font-size:11px;padding:3px 8px;background:#e8f5e9;color:#2e7d32;border-radius:12px;text-decoration:none;white-space:nowrap;';
      a.onmouseover = () => a.style.background = '#c8e6c9';
      a.onmouseout = () => a.style.background = '#e8f5e9';
      chips.appendChild(a);
    });

    linksDiv.appendChild(chips);
    contentEl.appendChild(linksDiv);
  }

  show('result-container');
}

function copyResult() {
  const text = document.getElementById('result-content').innerText;
  navigator.clipboard.writeText(text).then(() => {
    document.getElementById('btn-copy').textContent = '✅';
    setTimeout(() => { document.getElementById('btn-copy').textContent = '📋'; }, 1500);
  });
}

async function analyzeArticle(token, { title, url, content }, tabKey) {
  hide('result-container');
  hide('error-box');
  show('loading');
  document.getElementById('btn-analyze').disabled = true;

  // Add cancel button + elapsed timer
  const loadingEl = document.getElementById('loading');
  const cancelBtn = document.createElement('button');
  cancelBtn.id = 'btn-cancel';
  cancelBtn.textContent = '✕ Hủy';
  cancelBtn.style.cssText = 'margin-left:8px;background:none;border:1px solid #ccc;border-radius:6px;padding:2px 8px;cursor:pointer;font-size:11px;color:#666;';
  loadingEl.appendChild(cancelBtn);

  // Elapsed time counter
  const timerSpan = document.createElement('span');
  timerSpan.style.cssText = 'margin-left:6px;font-size:10px;color:#aaa;';
  loadingEl.appendChild(timerSpan);
  let elapsed = 0;
  const timerInterval = setInterval(() => {
    elapsed++;
    timerSpan.textContent = `${elapsed}s`;
    if (elapsed >= 15) timerSpan.style.color = '#e57373';
  }, 1000);

  const controller = new AbortController();
  cancelBtn.addEventListener('click', () => {
    controller.abort();
    clearInterval(timerInterval);
    chrome.storage.local.remove(['analyzing_tab', 'analyzing_start']);
    // Re-enable button with fresh onclick after cancel
    setTimeout(() => {
      btnAnalyze.disabled = false;
      btnAnalyze.innerHTML = '<span class="btn-icon">🤖</span> Phân tích bài viết bằng AI';
      btnAnalyze.onclick = () => analyzeArticle(token, { title, url, content }, tabKey);
    }, 50);
  });

  // 45 second timeout
  const timeoutId = setTimeout(() => controller.abort(), 45000);

  // Mark this tab as "analyzing" + save start time
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  await chrome.storage.local.set({ analyzing_tab: tab.id, analyzing_start: Date.now() });

  // Send request via background service worker to avoid popup throttling
  const requestData = {
    type: 'analyze-article',
    token,
    url,
    title,
    content,
  };

  try {
    const data = await new Promise((resolve, reject) => {
      chrome.runtime.sendMessage(requestData, (response) => {
        if (chrome.runtime.lastError) reject(new Error(chrome.runtime.lastError.message));
        else resolve(response);
      });
    });

    clearTimeout(timeoutId);
    clearInterval(timerInterval);

    await chrome.storage.local.remove(['analyzing_tab', 'analyzing_start']);

    if (data?.httpStatus === 403) {
      if (data.code === 'PREMIUM_REQUIRED') showSection('section-premium');
      else if (data.code === 'NO_CREDITS') showSection('section-no-credits');
      return;
    }

    if (data?.error) {
      showError(data.error || 'Có lỗi xảy ra. Vui lòng thử lại.');
      return;
    }

    if (!data?.success) {
      showError(data?.analysis || 'Không thể phân tích bài viết này.');
      return;
    }

    await chrome.storage.local.set({ [tabKey]: data });
    displayResult(data);

    // Update credits badge
    if (data.creditsRemaining !== undefined) {
      updateCreditsBadge(data.creditsRemaining);
    }

    const btn = document.getElementById('btn-analyze');
    btn.innerHTML = '<span class="btn-icon">🔄</span> Phân tích lại';

  } catch (err) {
    clearTimeout(timeoutId);
    clearInterval(timerInterval);
    await chrome.storage.local.remove(['analyzing_tab', 'analyzing_start']);
    if (err.name === 'AbortError') {
      showError('Đã hủy / hết thời gian chờ (45s). Vui lòng thử lại.');
    } else {
      showError('Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối mạng.');
    }
  } finally {
    clearInterval(timerInterval);
    hide('loading');
    cancelBtn.remove();
    timerSpan.remove();
    document.getElementById('btn-analyze').disabled = false;
  }
}

function showError(message) {
  document.getElementById('error-message').textContent = message;
  show('error-box');
}

// ── Content extractor (injected into page) ───────────────────────────────────

function extractArticleContent() {
  const title = document.title || '';

  // Clone body to strip noise (sidebar, nav, related articles) without mutating the page
  const bodyClone = document.body.cloneNode(true);
  const noiseSelectors = [
    'nav', 'header', 'footer', 'aside',
    '[class*="sidebar"]', '[class*="related"]', '[class*="recommend"]',
    '[class*="most-read"]', '[class*="tin-doc-nhieu"]', '[class*="box-"]',
    '[class*="widget"]', '[class*="advertisement"]', '[class*="ads"]',
    '[class*="social"]', '[class*="share"]', '[class*="comment"]',
    '[class*="tag"]', '[class*="breadcrumb"]',
  ];
  noiseSelectors.forEach(sel => {
    bodyClone.querySelectorAll(sel).forEach(el => el.remove());
  });

  // Try specific article selectors (ordered by specificity)
  const selectors = [
    '.article-body', '.article-content', '.article__body', '.article__content',
    '.post-content', '.post-body',
    '.entry-content', '.entry-body',
    '.content-detail', '.detail-content', '.detail__content',
    '.news-content', '.news-body',
    '.story-body', '.story-content',
    'article',
    '[itemprop="articleBody"]',
    '[class*="article-body"]', '[class*="article-content"]',
    '[class*="post-content"]', '[class*="entry-content"]',
    '[class*="content-detail"]', '[class*="detail-content"]',
  ];

  let content = '';
  for (const sel of selectors) {
    const el = bodyClone.querySelector(sel);
    if (el && el.innerText.length > 200) {
      content = el.innerText.trim();
      break;
    }
  }

  // Fallback: grab all paragraphs from cloned body
  if (!content) {
    const paragraphs = Array.from(bodyClone.querySelectorAll('p'))
      .map(p => p.innerText.trim())
      .filter(t => t.length > 50);
    content = paragraphs.join('\n\n');
  }

  return { title, content: content.slice(0, 5000) };
}

// ── History ───────────────────────────────────────────────────────────────────

let historyLoaded = false;

async function loadHistory() {
  if (historyLoaded) return;

  const token = await getToken();
  if (!token) return;

  const listEl = document.getElementById('history-list');
  const loadingEl = document.getElementById('history-loading');
  const emptyEl = document.getElementById('history-empty');

  try {
    const res = await fetch(`${API_BASE}/ai-analysis/history?page=1&pageSize=20&type=article`, {
      headers: { Authorization: `Bearer ${token}` },
    });

    if (!res.ok) throw new Error('Failed');

    const json = await res.json();
    const items = json.data || json;
    const articleItems = items.filter(i => i.analysisType === 'article');

    hide('history-loading');

    if (articleItems.length === 0) {
      show('history-empty');
      return;
    }

    listEl.innerHTML = '';
    articleItems.forEach(item => {
      const ctx = tryParseJson(item.contextJson);
      const title = ctx?.articleTitle || 'Bài viết không có tiêu đề';
      const league = ctx?.detectedLeague || 'Bóng đá Việt Nam';
      // Ensure UTC parsing by appending Z if missing
      const rawDate = item.createdAt.endsWith('Z') ? item.createdAt : item.createdAt + 'Z';
      const date = new Date(rawDate).toLocaleString('vi-VN', {
        timeZone: 'Asia/Ho_Chi_Minh',
        day: '2-digit', month: '2-digit', year: 'numeric',
        hour: '2-digit', minute: '2-digit',
      });

      const el = document.createElement('div');
      el.className = 'history-item';
      el.innerHTML = `
        <div class="history-item-title" title="${escapeHtml(title)}">${escapeHtml(title)}</div>
        <div class="history-item-meta">
          <span class="history-item-league">${escapeHtml(league)}</span>
          <span>${date}</span>
        </div>
      `;
      el.addEventListener('click', () => showHistoryDetail(item, title, league));
      listEl.appendChild(el);
    });

    show('history-list');
    historyLoaded = true;

  } catch {
    hide('history-loading');
    emptyEl.querySelector('p').textContent = 'Không thể tải lịch sử. Vui lòng thử lại.';
    show('history-empty');
  }
}

function showHistoryDetail(item, title, league) {
  const historyTab = document.getElementById('tab-history');
  historyTab.innerHTML = `
    <button class="history-back" id="btn-back-history">← Quay lại lịch sử</button>
    <div class="result-container" style="display:flex;max-height:360px;">
      <div class="result-header">
        <span class="result-league">${escapeHtml(league)}</span>
        <button class="btn-icon-only" id="btn-copy-history" title="Sao chép">📋</button>
      </div>
      <div class="result-content" id="history-detail-content">
        ${renderMarkdown(item.analysisVi.replace(/\[GIẢI ĐẤU:[^\]]+\]\s*/g, ''))}
      </div>
    </div>
  `;

  document.getElementById('btn-back-history').addEventListener('click', () => {
    historyLoaded = false;
    // Re-render history tab
    historyTab.innerHTML = `
      <div id="history-loading" class="loading">
        <div class="spinner"></div>
        <span>Đang tải lịch sử...</span>
      </div>
      <div id="history-empty" class="info-box info hidden">
        <span class="icon">📭</span>
        <p>Chưa có lịch sử phân tích nào.</p>
      </div>
      <div id="history-list" class="history-list hidden"></div>
    `;
    loadHistory();
  });

  document.getElementById('btn-copy-history').addEventListener('click', () => {
    const text = document.getElementById('history-detail-content').innerText;
    navigator.clipboard.writeText(text).then(() => {
      document.getElementById('btn-copy-history').textContent = '✅';
      setTimeout(() => { document.getElementById('btn-copy-history').textContent = '📋'; }, 1500);
    });
  });
}

function tryParseJson(str) {
  try { return str ? JSON.parse(str) : null; } catch { return null; }
}

function escapeHtml(str) {
  return String(str).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}

// ── Boot ─────────────────────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
  // Tab switching
  document.querySelectorAll('.tab-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
      document.querySelectorAll('.tab-content').forEach(t => t.classList.add('hidden'));
      btn.classList.add('active');
      document.getElementById(btn.dataset.tab)?.classList.remove('hidden');
      if (btn.dataset.tab === 'tab-history') loadHistory();
    });
  });

  init();
});
