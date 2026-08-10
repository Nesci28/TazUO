// SPDX-License-Identifier: BSD-2-Clause

namespace ClassicUO.Game.Managers.VendorSearch;

internal static class VendorSearchWebPage
{
    public const string Html = """
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>TazUO Vendor Search</title>
  <style>
    :root { color-scheme: dark; --gold:#d9bb72; --ink:#12151b; --panel:#20252d; --muted:#98a2b3; }
    * { box-sizing:border-box; }
    body { margin:0; min-height:100vh; font-family:Inter,ui-sans-serif,system-ui,-apple-system,sans-serif; color:#f3f0e8; background:radial-gradient(circle at 20% 0,#313944 0,#171b21 42%,#0d0f13 100%); }
    header { display:flex; align-items:center; justify-content:space-between; gap:20px; padding:22px clamp(18px,4vw,52px); border-bottom:1px solid #ffffff18; background:#101319d9; backdrop-filter:blur(12px); position:sticky; top:0; z-index:20; }
    h1 { margin:0; color:var(--gold); font:600 clamp(20px,3vw,30px) Georgia,serif; letter-spacing:.04em; }
    #connection { color:var(--muted); font-size:13px; }
    main { display:grid; grid-template-columns:minmax(0,1fr) 280px; gap:24px; max-width:1220px; margin:0 auto; padding:28px clamp(16px,3vw,36px) 48px; }
    .workspace { min-width:0; }
    .scroll { overflow:auto; padding:12px; border:1px solid #ffffff1a; border-radius:14px; background:#080a0d80; box-shadow:0 18px 50px #0008; }
    #stage { position:relative; min-width:320px; min-height:180px; overflow:hidden; border:1px solid #b89c5d66; border-radius:8px; background:linear-gradient(145deg,#312f2a,#1d2127 55%,#171a20); box-shadow:inset 0 0 55px #0009; }
    .text { position:absolute; overflow:hidden; white-space:pre-wrap; line-height:1.25; color:#e9dfc6; font-family:Georgia,serif; font-size:14px; pointer-events:none; }
    .entry { position:absolute; border:1px solid #c3a86388; border-radius:4px; background:#0c1016; color:#fff7df; padding:2px 6px; outline:none; box-shadow:inset 0 1px 4px #000a; }
    .entry:focus { border-color:#e5c671; box-shadow:0 0 0 2px #e5c67133; }
    .gump-button { position:absolute; display:grid; place-items:center; min-width:24px; min-height:18px; border:1px solid #d8bd7988; border-radius:5px; color:#f6dc9b; background:linear-gradient(#454a50,#24282e); cursor:pointer; font-weight:800; box-shadow:0 2px 4px #0008; }
    .gump-button:hover { filter:brightness(1.25); transform:translateY(-1px); }
    .gump-button:disabled { opacity:.45; cursor:wait; }
    .switch { position:absolute; display:flex; align-items:center; gap:6px; color:#eadfca; font:13px Georgia,serif; }
    .item { position:absolute; width:74px; height:66px; display:grid; place-items:center; }
    .item img { max-width:74px; max-height:66px; image-rendering:auto; filter:drop-shadow(0 3px 3px #000c); transform-origin:center; }
    .empty { display:grid; place-items:center; min-height:360px; padding:40px; text-align:center; color:var(--muted); }
    .empty strong { display:block; color:var(--gold); font:600 22px Georgia,serif; margin-bottom:10px; }
    aside { align-self:start; border:1px solid #ffffff17; border-radius:14px; padding:20px; background:#171b21dd; box-shadow:0 15px 42px #0006; }
    aside h2 { margin:0 0 12px; color:var(--gold); font:600 17px Georgia,serif; }
    aside p, aside li { color:#b7bfca; font-size:13px; line-height:1.55; }
    aside ol { padding-left:20px; margin-bottom:0; }
    #notice { min-height:22px; margin:0 2px 10px; color:#d9cfae; font-size:13px; }
    .spinner { width:30px; height:30px; margin:18px auto; border:3px solid #ffffff20; border-top-color:var(--gold); border-radius:50%; animation:spin .8s linear infinite; }
    @keyframes spin { to { transform:rotate(360deg); } }
    @media (max-width:850px) { main { grid-template-columns:1fr; } aside { order:-1; } }
  </style>
</head>
<body>
  <header><h1>TazUO Vendor Search</h1><div id="connection">Connecting to TazUO…</div></header>
  <main>
    <section class="workspace">
      <div id="notice" role="status"></div>
      <div class="scroll"><div id="stage" class="empty"><div><strong>Waiting for Vendor Search</strong>Open Vendor Search in TazUO to begin.</div></div></div>
    </section>
    <aside>
      <h2>How it works</h2>
      <p>This page mirrors the live OSI Vendor Search gump. Searches and map requests are still validated and executed by your shard.</p>
      <ol><li>Open your character's context menu.</li><li>Select <em>Vendor Search</em>.</li><li>Use this page; TazUO forwards each response to the current gump.</li></ol>
      <p>Keep TazUO connected. This server listens only on your local machine.</p>
    </aside>
  </main>
  <script>
    const stage = document.getElementById('stage');
    const notice = document.getElementById('notice');
    const connection = document.getElementById('connection');
    let current = null;
    let activePage = 1;
    let lastRevision = -1;
    let lastVersion = -1;
    let drafts = {};
    let switchDrafts = {};
    let submitting = false;

    const visible = control => control.page === 0 || control.page === activePage;
    const px = value => `${Math.max(0, value || 0)}px`;

    function positioned(tag, className, control) {
      const element = document.createElement(tag);
      element.className = className;
      element.style.left = px(control.x);
      element.style.top = px(control.y);
      if (control.width) element.style.width = px(control.width);
      if (control.height) element.style.height = px(control.height);
      return element;
    }

    function showEmpty(title, message, spinning = false) {
      stage.className = 'empty';
      stage.removeAttribute('style');
      stage.replaceChildren();
      const box = document.createElement('div');
      const heading = document.createElement('strong');
      heading.textContent = title;
      box.appendChild(heading);
      if (spinning) { const spin = document.createElement('div'); spin.className = 'spinner'; box.appendChild(spin); }
      const body = document.createElement('div');
      body.textContent = message || '';
      box.appendChild(body);
      stage.appendChild(box);
    }

    function render(state) {
      current = state;
      notice.textContent = state.message || '';

      if (!state.available) {
        showEmpty('Vendor Search is not open', state.message || 'Open Vendor Search in TazUO.');
        return;
      }
      if (state.mode === 'pending' || state.mode === 'waiting') {
        showEmpty('Searching vendors', state.message || 'Waiting for the shard…', true);
        return;
      }

      stage.className = '';
      stage.replaceChildren();
      stage.style.width = px(Math.max(320, state.width));
      stage.style.height = px(Math.max(180, state.height));

      for (const text of state.texts || []) {
        if (!visible(text)) continue;
        const element = positioned('div', 'text', text);
        element.textContent = text.text || '';
        stage.appendChild(element);
      }

      for (const item of state.items || []) {
        if (!visible(item)) continue;
        const holder = positioned('div', 'item', { ...item, width:74, height:66 });
        const image = document.createElement('img');
        image.src = item.artUrl;
        image.alt = item.name || `Item art ${item.graphic}`;
        image.style.transform = `scale(${item.scale || 1})`;
        const details = [item.name, item.properties].filter(Boolean).join('\n');
        if (details) holder.title = details;
        holder.appendChild(image);
        stage.appendChild(holder);
      }

      for (const entry of state.entries || []) {
        if (!visible(entry)) continue;
        const input = positioned('input', 'entry vendor-entry', entry);
        input.type = 'text';
        input.dataset.id = entry.id;
        input.maxLength = 239;
        input.value = Object.hasOwn(drafts, entry.id) ? drafts[entry.id] : (entry.text || '');
        input.addEventListener('input', () => { drafts[entry.id] = input.value; });
        stage.appendChild(input);
      }

      for (const choice of state.switches || []) {
        if (!visible(choice)) continue;
        const label = positioned('label', 'switch', choice);
        const input = document.createElement('input');
        input.type = 'checkbox';
        input.className = 'vendor-switch';
        input.dataset.id = choice.id;
        if (!Object.hasOwn(switchDrafts, choice.id)) switchDrafts[choice.id] = !!choice.isChecked;
        input.checked = switchDrafts[choice.id];
        input.addEventListener('change', () => { switchDrafts[choice.id] = input.checked; });
        const caption = document.createElement('span');
        caption.textContent = choice.text || '';
        label.append(input, caption);
        stage.appendChild(label);
      }

      for (const button of state.buttons || []) {
        if (!visible(button)) continue;
        const element = positioned('button', 'gump-button', button);
        element.type = 'button';
        element.title = button.tooltip || (button.isPageButton ? 'Open category' : 'Send response');
        element.setAttribute('aria-label', element.title);
        element.textContent = button.isPageButton ? '›' : (button.buttonID === 0 ? '×' : (state.mode === 'results' && button.buttonID >= 100 ? '↗' : '●'));
        element.disabled = submitting;
        element.addEventListener('click', () => {
          if (button.isPageButton) { activePage = button.toPage; render(current); }
          else submit(button.buttonID);
        });
        stage.appendChild(element);
      }
    }

    async function submit(buttonID) {
      if (!current || submitting) return;
      submitting = true;
      render(current);
      const entries = {};
      for (const entry of current.entries || []) {
        entries[entry.id] = Object.hasOwn(drafts, entry.id) ? drafts[entry.id] : (entry.text || '');
      }
      const switches = (current.switches || [])
        .filter(choice => Object.hasOwn(switchDrafts, choice.id) ? switchDrafts[choice.id] : choice.isChecked)
        .map(choice => choice.id);
      try {
        const response = await fetch('/api/vendor-search/respond', {
          method:'POST',
          headers:{'Content-Type':'application/json'},
          body:JSON.stringify({ version:current.version, buttonID, entries, switches })
        });
        const result = await response.json();
        if (!response.ok) notice.textContent = result.error || 'Vendor Search rejected the response.';
      } catch (error) {
        notice.textContent = `Unable to reach TazUO: ${error.message}`;
      } finally {
        submitting = false;
        await poll(true);
      }
    }

    async function poll(force = false) {
      try {
        const response = await fetch('/api/vendor-search', { cache:'no-store' });
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const state = await response.json();
        connection.textContent = 'Connected to TazUO';
        if (state.version !== lastVersion) {
          lastVersion = state.version;
          activePage = state.activePage || 1;
          drafts = {};
          switchDrafts = {};
        }
        if (force || state.revision !== lastRevision) {
          lastRevision = state.revision;
          render(state);
        }
      } catch (error) {
        connection.textContent = 'TazUO connection lost';
        if (!current) showEmpty('TazUO is unavailable', error.message);
      }
    }

    poll(true);
    setInterval(poll, 600);
  </script>
</body>
</html>
""";
}
