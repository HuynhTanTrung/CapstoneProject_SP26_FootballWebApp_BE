const API_BASE = 'http://localhost:5272';

chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.type === 'identify') {
    // Call both APIs in parallel
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
