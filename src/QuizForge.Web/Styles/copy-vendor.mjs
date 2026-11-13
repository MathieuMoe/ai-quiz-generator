// Copies the third-party browser files we need from node_modules into wwwroot/lib.
import { cpSync, mkdirSync } from "node:fs";

const files = [
  ["node_modules/preline/dist/preline.js", "wwwroot/lib/preline/preline.js"],
  ["node_modules/preline/LICENSE", "wwwroot/lib/preline/LICENSE"],
  ["node_modules/pdfjs-dist/build/pdf.min.mjs", "wwwroot/lib/pdfjs/pdf.min.mjs"],
  ["node_modules/pdfjs-dist/build/pdf.worker.min.mjs", "wwwroot/lib/pdfjs/pdf.worker.min.mjs"],
  ["node_modules/pdfjs-dist/LICENSE", "wwwroot/lib/pdfjs/LICENSE"],
];

for (const [from, to] of files) {
  mkdirSync(to.substring(0, to.lastIndexOf("/")), { recursive: true });
  cpSync(from, to);
  console.log(`copied ${from} -> ${to}`);
}
