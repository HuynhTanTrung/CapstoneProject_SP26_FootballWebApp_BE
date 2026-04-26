// const API_BASE = 'http://localhost:5272/api';
const API_BASE = 'https://footballwebappservice-cggdfkhcbsdzfnga.southeastasia-01.azurewebsites.net';

chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  // Save JWT token from web app
  if (request.type === 'SAVE_TOKEN') {
    chrome.storage.local.set({ vnfootball_token: request.token }, () => {
      sendResponse({ success: true });
    });
    return true;
  }

  // Clear token on logout
  if (request.type === 'CLEAR_TOKEN') {
    chrome.storage.local.remove('vnfootball_token', () => {
      sendResponse({ success: true });
    });
    return true;
  }

  // Analyze article — runs in background to avoid popup throttling
  if (request.type === 'analyze-article') {
    fetch(`${API_BASE}/api/ArticleAnalysis/analyze`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${request.token}`,
      },
      body: JSON.stringify({
        articleUrl: request.url,
        articleTitle: request.title,
        articleContent: request.content,
      }),
    })
    .then(async res => {
      const data = await res.json();
      if (!res.ok) {
        sendResponse({ error: data.message || 'Lỗi server', httpStatus: res.status, code: data.code, ...data });
      } else {
        sendResponse(data);
      }
    })
    .catch(err => sendResponse({ error: err.message }));
    return true; // keep channel open for async
  }

  if (request.type === 'identify') {
    // Call both APIs in parallel — use fuzzy match for speed (AI endpoint reserved for popup analysis)
    Promise.all([
      fetch(`${API_BASE}/api/Football/identify-players`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ text: request.text })
      }).then(r => r.json()).catch(() => null),

      fetch(`${API_BASE}/api/Football/identify-match`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ text: request.text })
      }).then(r => r.json()).catch(() => null)
    ]).then(([playerData, matchData]) => {
      sendResponse({ success: true, playerData, matchData });
    }).catch(err => sendResponse({ success: false, error: err.message }));
    return true;
  }

  if (request.type === 'identify-match-only') {
    fetch(`${API_BASE}/api/Football/identify-match`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text: request.text })
    })
      .then(r => r.json())
      .then(data => sendResponse({ success: data.success, match: data.match }))
      .catch(() => sendResponse({ success: false }));
    return true;
  }

  if (request.type === 'fetch-image') {
    fetch(request.url, { headers: { 'User-Agent': 'Mozilla/5.0', 'Referer': 'https://www.sofascore.com/' } })
      .then(res => res.blob())
      .then(blob => new Promise(resolve => {
        const reader = new FileReader();
        reader.onloadend = () => resolve(reader.result);
        reader.readAsDataURL(blob);
      }))
      .then(dataUrl => sendResponse({ success: true, dataUrl }))
      .catch(() => sendResponse({ success: false }));
    return true;
  }
});
