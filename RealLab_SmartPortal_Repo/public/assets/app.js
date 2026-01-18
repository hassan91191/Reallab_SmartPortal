/* Real Lab Smart Portal - Client */
(() => {
  const $ = (id) => document.getElementById(id);
  const state = {
    lab: null,
    pid: null,
    config: null,
    files: [],
  };

  const i18n = {
    ar: {
      portalTitle: 'بوابة النتائج الذكية',
      portalSubtitle: 'اعرض نتائج التحاليل بأمان وسهولة',
      hint: 'افتح الرابط من رسالة المعمل أو اكتب رقم/كود المريض هنا.',
      labKey: 'كود المعمل',
      patientId: 'رقم/كود المريض',
      load: 'تحميل النتائج',
      loading: 'جاري التحميل…',
      noFiles: 'لا توجد ملفات لهذا الرقم حتى الآن',
      view: 'عرض',
      open: 'فتح',
      updated: 'آخر تحديث',
      retry: 'إعادة المحاولة',
      invalidLinkTitle: 'الرابط غير مكتمل',
      invalidLinkDesc: 'تأكد أن الرابط يحتوي على كود المعمل (lab) ورقم المريض (id).',
      failedTitle: 'تعذر تحميل النتائج',
      failedDesc: 'من فضلك تأكد من صحة البيانات أو حاول مرة أخرى.',
      copied: 'تم النسخ',
      copy: 'نسخ',
      filePdf: 'PDF',
      fileImage: 'صورة',
      fileOther: 'ملف',
    },
    en: {
      portalTitle: 'Smart Results Portal',
      portalSubtitle: 'View lab results safely and easily',
      hint: 'Open the link from the lab message or enter your Patient ID here.',
      labKey: 'Lab Key',
      patientId: 'Patient ID',
      load: 'Load results',
      loading: 'Loading…',
      noFiles: 'No files found for this patient yet',
      view: 'View',
      open: 'Open',
      updated: 'Updated',
      retry: 'Retry',
      invalidLinkTitle: 'Link is incomplete',
      invalidLinkDesc: 'Make sure the link contains lab and id parameters.',
      failedTitle: 'Failed to load results',
      failedDesc: 'Please verify the details and try again.',
      copied: 'Copied',
      copy: 'Copy',
      filePdf: 'PDF',
      fileImage: 'Image',
      fileOther: 'File',
    }
  };

  const lang = (navigator.language || '').toLowerCase().startsWith('ar') ? 'ar' : 'ar';
  const t = i18n[lang];

  function setText(id, text) {
    const el = $(id);
    if (el) el.textContent = text;
  }

  function setLoading(isLoading) {
    $('loadingState').hidden = !isLoading;
    $('emptyState').hidden = true;
    $('errorState').hidden = true;
    $('filesGrid').innerHTML = '';
  }

  function setEmpty(msg) {
    $('loadingState').hidden = true;
    $('emptyState').hidden = false;
    $('errorState').hidden = true;
    $('filesGrid').innerHTML = '';
    setText('emptyText', msg || t.noFiles);
  }

  function setError(title, desc) {
    $('loadingState').hidden = true;
    $('emptyState').hidden = true;
    $('errorState').hidden = false;
    $('filesGrid').innerHTML = '';
    setText('errorTitle', title || t.failedTitle);
    setText('errorDesc', desc || t.failedDesc);
  }

  function formatDate(d) {
    try {
      const dt = new Date(d);
      if (Number.isNaN(dt.getTime())) return '';
      return dt.toLocaleString(lang === 'ar' ? 'ar-EG' : 'en-US', {
        year: 'numeric',
        month: 'short',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
      });
    } catch {
      return '';
    }
  }

  function fileKind(file) {
    const name = (file.name || '').toLowerCase();
    const mt = (file.mimeType || '').toLowerCase();
    if (mt.includes('pdf') || name.endsWith('.pdf')) return 'pdf';
    if (mt.startsWith('image/') || name.match(/\.(png|jpg|jpeg|webp|gif)$/)) return 'img';
    return 'other';
  }

  function iconSvg(kind) {
    if (kind === 'pdf') {
      return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M7 2h7l5 5v15a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2zm7 1.5V8h4.5" fill="none" stroke="currentColor" stroke-width="1.5"/><path d="M8 14h8M8 17h8" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>';
    }
    if (kind === 'img') {
      return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 6a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6z" fill="none" stroke="currentColor" stroke-width="1.5"/><path d="M8 10a1.5 1.5 0 1 0 0-.01V10z" fill="currentColor"/><path d="M4 18l5-5 4 4 3-3 4 4" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/></svg>';
    }
    return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M7 2h7l5 5v15a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2zm7 1.5V8h4.5" fill="none" stroke="currentColor" stroke-width="1.5"/><path d="M8 14h8" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>';
  }

  function escapeHtml(s) {
    return String(s || '').replace(/[&<>"]/g, (c) => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
  }

  async function apiGet(path) {
    const r = await fetch(path, { headers: { 'accept': 'application/json' } });
    const text = await r.text();
    let data = null;
    try { data = text ? JSON.parse(text) : null; } catch { data = { raw: text }; }
    if (!r.ok) {
      const msg = (data && (data.error || data.message)) ? (data.error || data.message) : `HTTP ${r.status}`;
      const err = new Error(msg);
      err.status = r.status;
      err.data = data;
      throw err;
    }
    return data;
  }

  async function apiPost(path, body) {
    const r = await fetch(path, {
      method: 'POST',
      headers: { 'content-type': 'application/json', 'accept': 'application/json' },
      body: JSON.stringify(body),
      keepalive: true
    });
    // Logging failures shouldn't block UX, so no throw here
    return r.ok;
  }

  function parseParams() {
    const u = new URL(location.href);
    const lab = (u.searchParams.get('lab') || '').trim();
    const id = (u.searchParams.get('id') || '').trim();
    return { lab, id };
  }

  function setParams(lab, id) {
    const u = new URL(location.href);
    u.searchParams.set('lab', lab);
    u.searchParams.set('id', id);
    history.replaceState({}, '', u.toString());
  }

  function applyStrings() {
    setText('portalTitle', t.portalTitle);
    setText('portalSubtitle', t.portalSubtitle);
    setText('hintText', t.hint);
    $('labInput').placeholder = t.labKey;
    $('pidInput').placeholder = t.patientId;
    setText('loadText', t.load);
    setText('loadingText', t.loading);
    setText('retryText', t.retry);
    setText('footerRight', 'تم التطوير لدعم معامل متعددة عبر LabKey');
  }

  async function loadAll() {
    const { lab, id } = parseParams();
    state.lab = lab;
    state.pid = id;

    $('labInput').value = lab || '';
    $('pidInput').value = id || '';

    if (!lab || !id) {
      setError(t.invalidLinkTitle, t.invalidLinkDesc);
      return;
    }

    setLoading(true);

    try {
      const cfg = await apiGet(`/.netlify/functions/get-lab-config?lab=${encodeURIComponent(lab)}`);
      state.config = cfg;

      // Branding
      const title = cfg.title || t.portalTitle;
      const subtitle = cfg.subtitle || t.portalSubtitle;
      setText('labTitle', title);
      setText('labSubtitle', subtitle);
      setText('portalTitle', title);
      setText('portalSubtitle', subtitle);

      if (cfg.logoFileId) {
        const logoUrl = `/.netlify/functions/download-file?lab=${encodeURIComponent(lab)}&fileId=${encodeURIComponent(cfg.logoFileId)}&logo=1`;
        $('labLogo').src = logoUrl;
        $('labLogo').hidden = false;
      } else {
        $('labLogo').hidden = true;
      }

      const res = await apiGet(`/.netlify/functions/get-files?lab=${encodeURIComponent(lab)}&id=${encodeURIComponent(id)}`);
      state.files = Array.isArray(res.files) ? res.files : [];

      if (!state.files.length) {
        setEmpty(t.noFiles);
        return;
      }

      $('loadingState').hidden = true;
      $('emptyState').hidden = true;
      $('errorState').hidden = true;

      renderFiles();

    } catch (e) {
      const desc = (e && e.message) ? e.message : t.failedDesc;
      setError(t.failedTitle, desc);
    }
  }

  function renderFiles() {
    const grid = $('filesGrid');
    grid.innerHTML = '';

    for (const f of state.files) {
      const kind = fileKind(f);
      const label = kind === 'pdf' ? t.filePdf : kind === 'img' ? t.fileImage : t.fileOther;
      const updated = f.modifiedTime ? formatDate(f.modifiedTime) : '';
      const safeName = escapeHtml(f.name || '');

      const downloadUrl = `/.netlify/functions/download-file?lab=${encodeURIComponent(state.lab)}&id=${encodeURIComponent(state.pid)}&fileId=${encodeURIComponent(f.id)}`;

      const card = document.createElement('div');
      card.className = 'file';
      card.innerHTML = `
        <div class="file__row">
          <div class="file__icon" aria-hidden="true">${iconSvg(kind)}</div>
          <div class="file__meta">
            <div class="file__name" title="${safeName}">${safeName}</div>
            <div class="file__sub">
              <span class="pill">${label}</span>
              ${updated ? `<span class="muted">${t.updated}: ${escapeHtml(updated)}</span>` : ``}
            </div>
          </div>
        </div>
        <div class="file__actions">
          <a class="btn btn--ghost" href="${downloadUrl}" target="_blank" rel="noopener" data-fileid="${escapeHtml(f.id)}" data-filename="${safeName}">
            ${t.view}
          </a>
        </div>
      `;

      const a = card.querySelector('a');
      a.addEventListener('click', () => {
        // fire-and-forget log
        apiPost('/.netlify/functions/log-access', {
          lab: state.lab,
          patientId: state.pid,
          fileId: f.id,
          fileName: f.name,
          action: 'VIEW',
          userAgent: navigator.userAgent
        });
      });

      grid.appendChild(card);
    }
  }

  // Wire UI
  function wire() {
    $('year').textContent = String(new Date().getFullYear());

    $('loadBtn').addEventListener('click', () => {
      const lab = ($('labInput').value || '').trim();
      const pid = ($('pidInput').value || '').trim();
      if (!lab || !pid) {
        setError(t.invalidLinkTitle, t.invalidLinkDesc);
        return;
      }
      setParams(lab, pid);
      loadAll();
    });

    $('retryBtn').addEventListener('click', () => loadAll());

    document.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && (document.activeElement === $('labInput') || document.activeElement === $('pidInput'))) {
        $('loadBtn').click();
      }
    });
  }

  applyStrings();
  wire();
  loadAll();
})();
