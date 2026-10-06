window.advisor = {
 openDiagram: () => document.getElementById('diagram-overview').showModal(),
 closeDiagram: () => document.getElementById('diagram-overview').close(),
 resetStepPane: () => { document.getElementById('implementation-step-pane').scrollTop = 0; },
 copy: async text => {
   if(navigator.clipboard) { try { await navigator.clipboard.writeText(text); return; } catch { /* Fall back for embedded browsers. */ } }
   // Synchronous fallback also works in embedded browsers without Clipboard API support.
   const area=document.createElement('textarea'); area.value=text; area.style.position='fixed'; area.style.opacity='0'; document.body.append(area);
   const prior=document.activeElement; area.select(); let copied=false;
   try { copied=document.execCommand('copy'); } finally { area.remove(); if(prior && prior.focus) prior.focus(); }
   if(copied) return;
   if(!navigator.clipboard) throw new Error('Clipboard unavailable');
   await Promise.race([navigator.clipboard.writeText(text),new Promise((_,reject)=>setTimeout(()=>reject(new Error('Clipboard unavailable')),2000))]);
 },
 download: (name, text) => { const url=URL.createObjectURL(new Blob([text],{type:'text/markdown;charset=utf-8'})); const a=document.createElement('a'); a.href=url; a.download=name; document.body.append(a); a.click(); a.remove(); setTimeout(()=>URL.revokeObjectURL(url),10000); }
};
