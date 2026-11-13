// QuizForge – imports a PDF / TXT / MD file or a web page and puts its text in the form.
// PDF text extraction runs in the browser with Mozilla pdf.js: documents are never stored on the server.
import * as pdfjs from "/lib/pdfjs/pdf.min.mjs";

// Translations rendered by the server (Resources/SharedResource.*.resx, keys "Js_*")
const strings = JSON.parse(document.getElementById("i18n")?.textContent || "{}");
const t = (key, ...args) => (strings[key] ?? key).replace(/\{(\d+)\}/g, (_, i) => args[Number(i)]);
const nf = new Intl.NumberFormat(document.documentElement.lang || undefined);

pdfjs.GlobalWorkerOptions.workerSrc = "/lib/pdfjs/pdf.worker.min.mjs";

const MAX_BYTES = 10 * 1024 * 1024;
const MAX_PAGES = 200;

const fileInput = document.getElementById("Input_File");
const textArea = document.getElementById("Input_Text");
const dropzone = document.getElementById("dropzone");
const status = document.getElementById("file-status");
const maxChars = Number(textArea?.getAttribute("maxlength")) || 30000;

const colors = {
  info: "text-muted-foreground-1",
  ok: "text-teal-600 dark:text-teal-400",
  error: "text-red-600 dark:text-red-400",
};

const setStatus = (element, message, kind = "info") => {
  element.className = `mt-2 text-xs ${colors[kind]}`;
  element.textContent = message;
  element.hidden = false;
};

const showStatus = (message, kind) => setStatus(status, message, kind);

/** Puts the imported text in the textarea (truncated to the maximum length) and returns a status suffix. */
const fillText = (text) => {
  let truncated = "";
  if (text.length > maxChars) {
    text = text.slice(0, maxChars);
    truncated = ` · ${t("Truncated", nf.format(maxChars))}`;
  }
  textArea.value = text;
  textArea.dispatchEvent(new Event("input"));
  return `${t("Characters", nf.format(text.length))}${truncated}`;
};

/** Extracts the text of a PDF, page by page, keeping line breaks. */
export async function extractPdfText(data, maxPages = MAX_PAGES) {
  const pdf = await pdfjs.getDocument({ data }).promise;
  const pages = Math.min(pdf.numPages, maxPages);
  const parts = [];
  for (let n = 1; n <= pages; n++) {
    const page = await pdf.getPage(n);
    const content = await page.getTextContent();
    let line = "";
    for (const item of content.items) {
      if (!("str" in item)) continue;
      line += item.str;
      if (item.hasEOL) {
        parts.push(line);
        line = "";
      } else if (item.str && !item.str.endsWith(" ")) {
        line += " ";
      }
    }
    if (line.trim()) parts.push(line);
    parts.push("");
  }
  const text = parts
    .join("\n")
    .replace(/[ \t]+/g, " ")
    .replace(/-\n(\p{Ll})/gu, "$1") // join words hyphenated at the end of a line
    .replace(/([^.!?:;\n])\n(?=\p{Ll})/gu, "$1 ") // join lines broken in the middle of a sentence
    .replace(/\n{3,}/g, "\n\n")
    .trim();
  return { text, pages, totalPages: pdf.numPages };
}

async function importFile(file) {
  if (!file) return;
  const name = file.name.toLowerCase();

  if (file.size > MAX_BYTES) {
    showStatus(t("TooLarge"), "error");
    return;
  }

  try {
    let text;
    let detail = "";
    if (name.endsWith(".pdf") || file.type === "application/pdf") {
      showStatus(t("Reading", file.name));
      const result = await extractPdfText(new Uint8Array(await file.arrayBuffer()));
      text = result.text;
      detail = ` · ${result.pages > 1 ? t("Pages", result.pages) : t("Page")}` +
        (result.totalPages > result.pages ? ` (${t("OfPages", result.totalPages)})` : "");
      if (!text) {
        showStatus(t("NoText"), "error");
        return;
      }
    } else if (name.endsWith(".txt") || name.endsWith(".md")) {
      text = (await file.text()).trim();
    } else {
      showStatus(t("Unsupported"), "error");
      return;
    }

    showStatus(`✓ ${file.name}${detail} · ${fillText(text)}`, "ok");
  } catch (err) {
    console.error(err);
    showStatus(t("ReadError"), "error");
  } finally {
    // The text is now in the textarea: no need to upload the file itself.
    fileInput.value = "";
  }
}

if (fileInput && textArea && dropzone) {
  fileInput.addEventListener("change", () => importFile(fileInput.files[0]));

  const highlight = (on) => dropzone.classList.toggle("border-primary", on);
  ["dragenter", "dragover"].forEach((e) =>
    dropzone.addEventListener(e, (ev) => { ev.preventDefault(); highlight(true); }));
  ["dragleave", "drop"].forEach((e) =>
    dropzone.addEventListener(e, (ev) => { ev.preventDefault(); highlight(false); }));
  dropzone.addEventListener("drop", (ev) => importFile(ev.dataTransfer.files[0]));
}

// ---------- Web page import ----------
const urlInput = document.getElementById("import-url");
const importBtn = document.getElementById("import-btn");
const urlStatus = document.getElementById("url-status");

async function importUrl() {
  const url = urlInput.value.trim();
  if (!url || !/^(https?:\/\/)?[^\s/$.?#][^\s]*\.[^\s]+/i.test(url)) {
    setStatus(urlStatus, t("invalid_url"), "error");
    return;
  }

  importBtn.classList.add("loading");
  importBtn.disabled = true;
  setStatus(urlStatus, t("Loading"));
  try {
    const response = await fetch("/api/import-url", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ url }),
    });
    const data = await response.json().catch(() => ({}));
    if (!response.ok) {
      const code = response.status === 429 ? "rate_limited" : data.code || "fetch_failed";
      setStatus(urlStatus, t(code), "error");
      return;
    }
    const suffix = fillText(data.text) + (data.truncated ? ` · ${t("Truncated", nf.format(maxChars))}` : "");
    setStatus(urlStatus, `✓ ${data.title} · ${suffix}`, "ok");
  } catch (err) {
    console.error(err);
    setStatus(urlStatus, t("fetch_failed"), "error");
  } finally {
    importBtn.classList.remove("loading");
    importBtn.disabled = false;
  }
}

if (urlInput && importBtn && urlStatus) {
  importBtn.addEventListener("click", importUrl);
  urlInput.addEventListener("keydown", (e) => {
    if (e.key === "Enter") {
      e.preventDefault(); // don't submit the quiz form
      importUrl();
    }
  });
}
