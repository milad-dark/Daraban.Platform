// Builds docs/_build/combined.html from the six Daraban certification documents.
// Rendered to PDF with headless Edge: mermaid diagrams are drawn client-side by
// mermaid.js, then --virtual-time-budget lets them settle before print-to-pdf.
const fs = require('fs');
const path = require('path');

const ROOT = path.resolve(__dirname, '..');
const DOCS = [
  { file: '01-Software-Architecture.md', title: 'مستند معماری نرم‌افزار', desc: 'فرضیات معماری، دیدگاه‌های منطقی/مورد کاربرد/پیاده‌سازی/استقرار/فرآیندی، کارایی، مقیاس‌پذیری و امنیت' },
  { file: '02-Requirements.md', title: 'مستند نیازمندی‌ها', desc: 'نیازمندی‌های کارکردی، غیرکارکردی و نیازمندی‌ها/فرضیات دامنه‌ای' },
  { file: '03-Test-Guide.md', title: 'مستند راهنمای تست', desc: 'استراتژی تست، پوشش آزمون‌ها، نحوه اجرا و سناریوهای تست دستی' },
  { file: '04-User-Guide.md', title: 'مستند راهنمای کاربری', desc: 'راهنمای ورود، داشبورد و سه ماژول اصلی همراه با تصاویر' },
  { file: '05-Declaration.md', title: 'اقرارنامه', desc: 'مشخصات متقاضی، نوع گواهی، فهرست مستندات و تعهدات' },
  { file: '06-Not-Implemented.md', title: 'قابلیت‌های پیاده‌سازی‌نشده', desc: 'وضعیت فعلی ماژول‌ها و برنامه توسعه آینده' },
];

const esc = (s) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

function inline(text) {
  // image placeholders (user guide): the real screenshots are not present yet
  text = text.replace(/!\[([^\]]*)\]\(([^)]*)\)/g, (m, alt, src) =>
    `<span class="imgph"><span class="imgph-label">${esc(alt)}</span><span class="imgph-note">تصویر «${esc(path.basename(src))}» — در نسخه نهایی جایگذاری شود</span></span>`);
  // links
  text = text.replace(/\[([^\]]+)\]\(([^)]+)\)/g, (m, t, u) => `<a href="${esc(u)}">${esc(t)}</a>`);
  // inline code (before bold so ** inside code stays literal)
  text = text.replace(/`([^`]+)`/g, (m, c) => `<code>${esc(c)}</code>`);
  // bold / italic (non-greedy + dotAll so **text with * in it** like `*.Api` still matches)
  text = text.replace(/\*\*(.+?)\*\*/gs, '<strong>$1</strong>');
  text = text.replace(/(^|[^*])\*([^*\n]+)\*/g, '$1<em>$2</em>');
  // checkboxes
  text = text.replace(/\[ \]/g, '<span class="cb">☐</span>');
  text = text.replace(/\[x\]/g, '<span class="cb">☑</span>');
  return text;
}

function convertMd(md) {
  const lines = md.split(/\r?\n/);
  const out = [];
  let i = 0;
  while (i < lines.length) {
    const line = lines[i];
    // fenced code
    if (/^```/.test(line)) {
      const lang = line.slice(3).trim();
      const buf = [];
      i++;
      while (i < lines.length && !/^```/.test(lines[i])) { buf.push(lines[i]); i++; }
      i++;
      const code = buf.join('\n');
      if (lang === 'mermaid') out.push(`<pre class="mermaid">${esc(code)}</pre>`);
      else out.push(`<pre class="codeblock"><code>${esc(code)}</code></pre>`);
      continue;
    }
    // headings
    const hm = line.match(/^(#{1,6})\s+(.*)$/);
    if (hm) {
      const lvl = Math.min(hm[1].length, 4);
      out.push(`<h${lvl}>${inline(hm[2])}</h${lvl}>`);
      i++;
      continue;
    }
    // horizontal rule (not a table row)
    if (/^\s*---\s*$/.test(line) && !/^\s*\|/.test(line)) {
      out.push('<hr/>');
      i++;
      continue;
    }
    // table
    if (/^\s*\|/.test(line) && i + 1 < lines.length && /^\s*\|[\s:|-]+\|\s*$/.test(lines[i + 1])) {
      const header = line.trim();
      const rows = [];
      i += 2;
      while (i < lines.length && /^\s*\|/.test(lines[i])) { rows.push(lines[i].trim()); i++; }
      const cells = (r) => r.replace(/^\|/, '').replace(/\|$/, '').split('|').map((c) => c.trim());
      let html = '<table><thead><tr>' + cells(header).map((c) => `<th>${inline(c)}</th>`).join('') + '</tr></thead>';
      html += '<tbody>' + rows.map((r) => '<tr>' + cells(r).map((c) => `<td>${inline(c)}</td>`).join('') + '</tr>').join('') + '</tbody></table>';
      out.push(html);
      continue;
    }
    // blockquote
    if (/^\s*>\s?/.test(line)) {
      const buf = [];
      while (i < lines.length && /^\s*>\s?/.test(lines[i])) { buf.push(lines[i].replace(/^\s*>\s?/, '')); i++; }
      out.push(`<blockquote>${inline(buf.join(' '))}</blockquote>`);
      continue;
    }
    // lists
    if (/^\s*[-*]\s+/.test(line) || /^\s*\d+\.\s+/.test(line)) {
      const ordered = /^\s*\d+\.\s+/.test(line);
      const items = [];
      while (i < lines.length && (/^\s*[-*]\s+/.test(lines[i]) || /^\s*\d+\.\s+/.test(lines[i]))) {
        const l = lines[i];
        const item = l.replace(/^\s*[-*]\s+/, '').replace(/^\s*\d+\.\s+/, '');
        items.push(item);
        i++;
      }
      const tag = ordered ? 'ol' : 'ul';
      out.push(`<${tag}>` + items.map((it) => `<li>${inline(it)}</li>`).join('') + `</${tag}>`);
      continue;
    }
    // blank line
    if (!line.trim()) { i++; continue; }
    // paragraph
    const buf = [line];
    i++;
    while (i < lines.length && lines[i].trim()
      && !/^```/.test(lines[i]) && !/^(#{1,6})\s/.test(lines[i])
      && !/^\s*\|/.test(lines[i]) && !/^\s*[-*]\s+/.test(lines[i])
      && !/^\s*\d+\.\s+/.test(lines[i]) && !/^\s*>\s?/.test(lines[i])) {
      buf.push(lines[i]);
      i++;
    }
    out.push(`<p>${inline(buf.join(' '))}</p>`);
  }
  return out.join('\n');
}

// ---- cover ----
const cover = `
<div class="cover">
  <div class="cover-brand">سامانه دارابان</div>
  <div class="cover-sub">Daraban Platform</div>
  <h1 class="cover-title">مجموعه مستندات رسمی</h1>
  <div class="cover-cert">بسته ارزیابی و اخذ گواهی نرم‌افزار</div>
  <div class="cover-meta">نسخه ۱٫۰ &nbsp;·&nbsp; شهریور ۱۴۰۵</div>
  <table class="cover-toc">
    <thead><tr><th style="width:10%">شماره</th><th style="width:35%">مستند</th><th>شرح</th></tr></thead>
    <tbody>
      ${DOCS.map((d, idx) => `<tr><td>${idx + 1}</td><td>${d.title}</td><td>${d.desc}</td></tr>`).join('')}
    </tbody>
  </table>
  <div class="cover-note">این مجموعه شامل مستندات رسمی سامانه دارابان برای فرآیند ارزیابی کیفیت نرم‌افزار است.</div>
</div>
`;

// ---- body ----
const body = DOCS.map((d, idx) => {
  const md = fs.readFileSync(path.join(ROOT, d.file), 'utf8');
  const html = convertMd(md);
  return `<section class="doc">
    <div class="docbar">مستند ${idx + 1} از ${DOCS.length} — ${d.title}</div>
    ${html}
  </section>`;
}).join('\n');

// ---- self-contained assets (the preview server and file:// both need them inline) ----
const mermaidJs = fs.readFileSync(path.join(__dirname, 'node_modules/mermaid/dist/mermaid.min.js'), 'utf8');
const font = (name) => 'data:font/woff2;base64,' + fs.readFileSync(
  path.join(__dirname, `node_modules/vazirmatn/fonts/webfonts/${name}.woff2`)).toString('base64');

const html = `<!DOCTYPE html>
<html dir="rtl" lang="fa">
<head>
<meta charset="utf-8"/>
<title>سامانه دارابان — مجموعه مستندات رسمی</title>
<style>
  @font-face { font-family:'Vazirmatn'; font-weight:400; src:url('${font('Vazirmatn-Regular')}'); }
  @font-face { font-family:'Vazirmatn'; font-weight:500; src:url('${font('Vazirmatn-Medium')}'); }
  @font-face { font-family:'Vazirmatn'; font-weight:700; src:url('${font('Vazirmatn-Bold')}'); }
  @page { size: A4; margin: 16mm 14mm 18mm 14mm; }
  * { box-sizing: border-box; }
  html { -webkit-print-color-adjust: exact; print-color-adjust: exact; }
  body { font-family:'Vazirmatn','Segoe UI',Tahoma,sans-serif; direction:rtl; font-size:10.5pt;
         line-height:1.8; color:#1b1b1b; margin:0; }
  h1 { font-size:17pt; color:#16324f; margin:18px 0 10px; border-bottom:2px solid #16324f; padding-bottom:6px; }
  h2 { font-size:13.5pt; color:#1f4e79; margin:16px 0 8px; }
  h3 { font-size:11.5pt; color:#2b5c9e; margin:12px 0 6px; }
  h4 { font-size:10.5pt; color:#333; margin:10px 0 5px; }
  p { margin:6px 0; text-align:justify; }
  ul, ol { margin:6px 0; padding-right:22px; }
  li { margin:2px 0; }
  a { color:#1f4e79; text-decoration:none; }
  code { font-family:Consolas,'Courier New',monospace; font-size:8.5pt; background:#f2f2f2;
         padding:1px 5px; border-radius:3px; direction:ltr; unicode-bidi:embed; }
  pre.codeblock { direction:ltr; text-align:left; background:#f7f7f7; border:1px solid #e0e0e0;
         border-radius:6px; padding:10px 12px; font-size:8.5pt; line-height:1.5;
         white-space:pre-wrap; word-break:break-word; font-family:Consolas,'Courier New',monospace; }
  pre.mermaid { direction:ltr; text-align:center; background:#fff; border:1px solid #e3e3e3;
         border-radius:8px; padding:10px; margin:10px 0; overflow:hidden; }
  pre.mermaid svg { max-width:100%; height:auto; }
  table { width:100%; border-collapse:collapse; margin:8px 0; font-size:9.3pt; }
  th, td { border:1px solid #c6c6c6; padding:4px 8px; text-align:right; vertical-align:top; }
  th { background:#e9eef5; font-weight:700; color:#16324f; }
  tr:nth-child(even) td { background:#fafbfd; }
  blockquote { border-right:3px solid #2b5c9e; background:#f4f8fc; margin:8px 0;
         padding:6px 14px; border-radius:4px; color:#333; }
  hr { border:none; border-top:1px solid #cfcfcf; margin:14px 0; }
  section.doc { page-break-before:always; }
  .docbar { background:#16324f; color:#fff; padding:6px 14px; border-radius:5px;
         margin-bottom:14px; font-size:9.5pt; font-weight:500; }
  .imgph { display:block; border:2px dashed #9db4d0; border-radius:6px; padding:16px;
         margin:8px 0; text-align:center; color:#4a6a8a; background:#f7fafd; }
  .imgph .imgph-label { display:block; font-weight:700; margin-bottom:4px; }
  .imgph .imgph-note { font-size:8.5pt; }
  .cb { color:#1f4e79; }
  footer.pagefoot { position:fixed; bottom:0; left:0; right:0; text-align:center;
         font-size:8pt; color:#8a8a8a; padding:2px 0; }
  /* cover */
  .cover { text-align:center; padding-top:34mm; page-break-after:always; }
  .cover-brand { font-size:30pt; font-weight:700; color:#16324f; }
  .cover-sub { font-size:13pt; color:#2b5c9e; letter-spacing:2px; margin-top:4px; }
  .cover-title { font-size:19pt; color:#16324f; border:none; margin-top:26px; }
  .cover-cert { font-size:12pt; color:#555; margin-top:6px; }
  .cover-meta { font-size:10.5pt; color:#666; margin:14px 0 26px; }
  .cover-toc { margin-top:8px; }
  .cover-note { margin-top:26px; font-size:9.5pt; color:#777; }
</style>
</head>
<body>
${cover}
${body}
<footer class="pagefoot">سامانه دارابان (Daraban) — مجموعه مستندات رسمی — نسخه ۱٫۰</footer>
<script>${mermaidJs}</script>
<script>
  mermaid.initialize({
    startOnLoad: false,
    theme: 'neutral',
    securityLevel: 'loose',
    fontFamily: 'Vazirmatn, sans-serif',
    flowchart: { htmlLabels: true, curve: 'basis', nodeSpacing: 25, rankSpacing: 35, padding: 6 },
    sequence: { useMaxWidth: true, actorMargin: 30, messageMargin: 26, boxMargin: 6, width: 100, mirrorActors: false }
  });
  // Scale each rendered diagram so it fits entirely inside one A4 content box
  // (content area 182mm × 263mm minus the 11px pre padding/border, at 96dpi).
  const fitDiagramsToPage = () => {
    const usableW = (182 / 25.4) * 96 - 22;
    const usableH = (263 / 25.4) * 96 - 22;
    document.querySelectorAll('.mermaid svg').forEach((svg) => {
      const vb = svg.getAttribute('viewBox');
      if (!vb) return;
      const parts = vb.trim().split(/[ ,]+/).map(Number);
      if (parts.length < 4 || !parts[2] || !parts[3]) return;
      const scale = Math.min(1, usableW / parts[2], usableH / parts[3]);
      svg.setAttribute('width', Math.round(parts[2] * scale));
      svg.setAttribute('height', Math.round(parts[3] * scale));
    });
  };
  window.addEventListener('load', async () => {
    try {
      await mermaid.run({ nodes: document.querySelectorAll('pre.mermaid') });
    } catch (e) {
      document.body.dataset.mermaidError = String(e);
    }
    fitDiagramsToPage();
    document.body.dataset.ready = 'true';
    setTimeout(() => { document.body.dataset.settled = 'true'; }, 2000);
  });
</script>
</body>
</html>`;

fs.writeFileSync(path.join(__dirname, 'combined.html'), html);
console.log('combined.html written:', (html.length / 1024).toFixed(0) + ' KB');