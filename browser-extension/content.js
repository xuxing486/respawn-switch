function visibleVideo() {
  const candidates = [...document.querySelectorAll('video')]
    .map(video => ({ video, rect: video.getBoundingClientRect() }))
    .filter(x => x.rect.width > 120 && x.rect.height > 120 && getComputedStyle(x.video).visibility !== 'hidden')
    .sort((a, b) => b.rect.width * b.rect.height - a.rect.width * a.rect.height);
  return candidates[0]?.video ?? null;
}

chrome.runtime.onMessage.addListener((request, _sender, respond) => {
  (async () => {
    const video = visibleVideo();
    if (!video) return { ok: false, state: 'no-video', errorCode: 'no-video' };
    if (request.command === 'play') {
      try { await video.play(); } catch { return { ok: false, state: 'paused', errorCode: 'play-rejected' }; }
      return { ok: !video.paused, state: video.paused ? 'paused' : 'playing', errorCode: video.paused ? 'play-not-verified' : '' };
    }
    if (request.command === 'pause') {
      video.pause();
      return { ok: video.paused, state: video.paused ? 'paused' : 'playing', errorCode: video.paused ? '' : 'pause-not-verified' };
    }
    return { ok: true, state: video.paused ? 'paused' : 'playing', errorCode: '' };
  })().then(respond).catch(() => respond({ ok: false, state: 'error', errorCode: 'content-error' }));
  return true;
});
