const bridge = 'http://127.0.0.1:17653/respawnswitch';
let lastSequence = 0;
let lastHeartbeat = 0;

async function douyinTabs() {
  const tabs = await chrome.tabs.query({ url: ['https://www.douyin.com/*', 'https://*.douyin.com/*'] });
  return tabs.filter(tab => Number.isInteger(tab.id));
}

async function postStatus(payload) {
  try {
    await fetch(`${bridge}/status`, { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(payload) });
  } catch { }
}

async function execute(command) {
  const tabs = await douyinTabs();
  const base = { sequence: command.sequence, browser: navigator.userAgent.includes('Edg/') ? 'Edge' : 'Chrome', tabCount: tabs.length };
  if (tabs.length !== 1) {
    await postStatus({ ...base, ok: false, state: 'ambiguous', errorCode: tabs.length ? 'multiple-tabs' : 'no-tab' });
    return;
  }
  const tab = tabs[0];
  if (command.command === 'play') {
    await chrome.windows.update(tab.windowId, { focused: true });
    await chrome.tabs.update(tab.id, { active: true });
  }
  try {
    const result = await chrome.tabs.sendMessage(tab.id, { command: command.command });
    await postStatus({ ...base, ...result });
  } catch {
    await postStatus({ ...base, ok: false, state: 'unreachable', errorCode: 'content-unreachable' });
  }
}

async function poll() {
  try {
    const response = await fetch(`${bridge}/command?after=${lastSequence}`, { cache: 'no-store' });
    const command = await response.json();
    if (command?.sequence > lastSequence) { lastSequence = command.sequence; await execute(command); }
    else if (Date.now() - lastHeartbeat > 4000) { lastHeartbeat = Date.now(); await execute({ sequence: lastSequence, command: 'probe' }); }
  } catch { }
}

chrome.runtime.onInstalled.addListener(() => chrome.alarms.create('keepalive', { periodInMinutes: 0.5 }));
chrome.alarms.onAlarm.addListener(poll);
setInterval(poll, 500);
poll();
