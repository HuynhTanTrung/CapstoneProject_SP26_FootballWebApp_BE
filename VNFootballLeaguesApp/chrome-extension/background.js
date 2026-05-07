const API_BASE = 'https://footballwebappservice-cggdfkhcbsdzfnga.southeastasia-01.azurewebsites.net';
// const API_BASE = 'http://localhost:5272';

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
    fetch(`${API_BASE}/api/article-analysis/analyze`, {
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
      let data;
      try {
        data = await res.json();
      } catch {
        sendResponse({ error: `Lỗi server (${res.status}): Không thể đọc phản hồi.` });
        return;
      }
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
    // Call all 3 APIs in parallel — players, match, team
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
      }).then(r => r.json()).catch(() => null),

      fetch(`${API_BASE}/api/Football/identify-team`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ text: request.text })
      }).then(r => r.json()).catch(() => null),
    ]).then(([playerData, matchData, teamData]) => {
      sendResponse({ success: true, playerData, matchData, teamData });
    }).catch(err => sendResponse({ success: false, error: err.message }));
    return true;
  }

  // identify-article: smart detection for full article pages
  // Step 1: try match from lead text; Step 2: if no match, get teams+players from full text
  if (request.type === 'identify-article') {
    const leadText = request.leadText || request.fullText || '';
    const fullText = request.fullText || '';

    // Try match from first 600 chars of article (enough to find both teams, avoids sidebar)
    const matchText = fullText.substring(0, 600);
    fetch(`${API_BASE}/api/Football/identify-match`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text: matchText })
    })
    .then(r => r.json())
    .then(async matchData => {
      const hasMatch = matchData?.success && matchData?.match;

      if (hasMatch) {
        // Clear match found — return it, skip players/teams
        sendResponse({ success: true, mode: 'match', matchData, playerData: null, teamData: null });
      } else {
        // No clear match — get teams and players from article text (limit to avoid sidebar)
        const articleText = fullText.substring(0, 800);
        const [playerData, teamData] = await Promise.all([
          fetch(`${API_BASE}/api/Football/identify-players`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ text: articleText })
          }).then(r => r.json()).catch(() => null),

          fetch(`${API_BASE}/api/Football/identify-team`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ text: articleText })
          }).then(r => r.json()).catch(() => null),
        ]);
        sendResponse({ success: true, mode: 'entities', matchData: null, playerData, teamData });
      }
    })
    .catch(err => sendResponse({ success: false, error: err.message }));
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
