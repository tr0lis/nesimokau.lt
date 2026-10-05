window.nesimokau = window.nesimokau || {};

window.nesimokau.setTheme = function (theme) {
    const isDark = theme === 'dark';
    document.documentElement.dataset.theme = isDark ? 'dark' : 'light';
    document.documentElement.style.colorScheme = isDark ? 'dark' : 'light';
    if (document.body) {
        document.body.classList.toggle('dark-theme', isDark);
    }
};

window.nesimokau.downloadDiagnosticReport = function (reportTitle, accuracy, questions, durationSeconds, wrongAnswers) {
    const canvas = document.createElement("canvas"); canvas.width = 1190; canvas.height = 1684;
    const ctx = canvas.getContext("2d"); ctx.fillStyle = "#ffffff"; ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.fillStyle = "#7054e8"; ctx.fillRect(0, 0, canvas.width, 245); ctx.fillStyle = "#ffffff"; ctx.font = "bold 58px Arial"; ctx.fillText("nesimokau.lt", 90, 105); ctx.font = "34px Arial"; ctx.fillText(reportTitle, 90, 175);
    const card = (x, title, value, color = "#26203b") => { ctx.fillStyle = "#f0ebff"; ctx.fillRect(x, 330, 470, 180); ctx.fillStyle = "#5e5870"; ctx.font = "24px Arial"; ctx.fillText(title, x + 35, 385); ctx.fillStyle = color; ctx.font = "bold 52px Arial"; ctx.fillText(value, x + 35, 465); };
    card(90, "TIKSLUMAS", `${accuracy}%`, "#7054e8"); card(630, "KLAUSIMŲ", `${questions}`);
    ctx.fillStyle = "#26203b"; ctx.font = "bold 34px Arial"; ctx.fillText("Rezultatų diagrama", 90, 620); ctx.fillStyle = "#e8e2f6"; ctx.fillRect(90, 660, 1010, 45); ctx.fillStyle = "#7054e8"; ctx.fillRect(90, 660, 1010 * accuracy / 100, 45); ctx.fillStyle = "#5e5870"; ctx.font = "24px Arial"; ctx.fillText(`${accuracy}% teisingų atsakymų`, 90, 750);
    const guidance = accuracy >= 90 ? "Puikiai sekasi. Toliau rinkis sudėtingesnius pratimus." : accuracy >= 70 ? "Rekomenduojama kartoti diktantą ir praleistas raides." : "Pradėk nuo praleistų raidžių, tada treniruokis diktante.";
    ctx.fillStyle = "#26203b"; ctx.font = "bold 34px Arial"; ctx.fillText("Kur tobulėti", 90, 875); ctx.fillStyle = "#5e5870"; ctx.font = "26px Arial"; ctx.fillText(guidance, 90, 925); ctx.fillText(`Klaidingų atsakymų: ${wrongAnswers}`, 90, 980); ctx.fillText(`Trukmė: ${Math.floor(durationSeconds / 60)} min. ${durationSeconds % 60} sek.`, 90, 1030);
    const imageBytes = Uint8Array.from(atob(canvas.toDataURL("image/jpeg", .92).split(",")[1]), char => char.charCodeAt(0)); const encoder = new TextEncoder();
    const chunks = []; let length = 0; const add = value => { const data = typeof value === "string" ? encoder.encode(value) : value; chunks.push(data); length += data.length; }; const offsets = [0];
    add("%PDF-1.4\n"); const addObject = value => { offsets.push(length); add(`${offsets.length - 1} 0 obj\n${value}\nendobj\n`); };
    addObject("<< /Type /Catalog /Pages 2 0 R >>"); addObject("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"); addObject("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >>");
    offsets.push(length); add(`4 0 obj\n<< /Type /XObject /Subtype /Image /Width 1190 /Height 1684 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length ${imageBytes.length} >>\nstream\n`); add(imageBytes); add("\nendstream\nendobj\n");
    addObject("<< /Length 28 >>\nstream\nq 595 0 0 842 0 0 cm /Im0 Do Q\nendstream"); const xref = length; add(`xref\n0 ${offsets.length}\n0000000000 65535 f \n${offsets.slice(1).map(offset => `${offset.toString().padStart(10, "0")} 00000 n \n`).join("")}trailer\n<< /Size ${offsets.length} /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF`);
    const url = URL.createObjectURL(new Blob(chunks, { type: "application/pdf" })); const link = document.createElement("a"); link.href = url; link.download = `${reportTitle}.pdf`; link.click(); URL.revokeObjectURL(url); return;
};

window.nesimokau.speak = function (text) {
    if (!('speechSynthesis' in window)) return;
    window.speechSynthesis.cancel();
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = 'lt-LT';
    utterance.rate = 0.82;
    utterance.pitch = 1;
    window.speechSynthesis.speak(utterance);
};

window.nesimokau.celebrateLevelUp = function () {
    const root = document.createElement('div');
    root.className = 'confetti-root';
    document.body.appendChild(root);

    for (let i = 0; i < 120; i++) {
        const piece = document.createElement('span');
        piece.className = 'confetti-piece';
        piece.style.left = `${Math.random() * 100}vw`;
        piece.style.animationDelay = `${Math.random() * 350}ms`;
        piece.style.background = ['#7454ed', '#ff8b5e', '#51c9ad', '#5f9cf6', '#ffd77d'][i % 5];
        piece.style.transform = `rotate(${Math.random() * 360}deg)`;
        root.appendChild(piece);
    }

    setTimeout(() => root.remove(), 2800);
};

window.nesimokau.celebrateMicroComplete = function (key) {
    const root = document.createElement('div');
    root.className = 'micro-confetti-root';
    root.dataset.key = key || '';
    document.body.appendChild(root);

    for (let i = 0; i < 26; i++) {
        const piece = document.createElement('span');
        piece.className = 'micro-confetti-piece';
        piece.style.left = `${42 + Math.random() * 16}vw`;
        piece.style.animationDelay = `${Math.random() * 120}ms`;
        piece.style.background = ['#47c47e', '#54d0bf', '#ffd77d', '#7454ed'][i % 4];
        piece.style.transform = `rotate(${Math.random() * 360}deg)`;
        root.appendChild(piece);
    }

    setTimeout(() => root.remove(), 1200);
};
