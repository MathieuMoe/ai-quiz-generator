// MatGPT Quiz – quiz interactions (vanilla JS)
(() => {
  "use strict";

  // Character counter
  const text = document.getElementById("Input_Text");
  const counter = document.getElementById("char-count");
  if (text && counter) {
    const update = () => (counter.textContent = text.value.length.toLocaleString());
    text.addEventListener("input", update);
    update();
  }

  // Loading state on submit
  document.getElementById("quiz-form")?.addEventListener("submit", () => {
    const btn = document.getElementById("generate-btn");
    btn.classList.add("loading");
    btn.disabled = true;
  });

  const quiz = document.getElementById("quiz");
  if (!quiz) return;

  const questions = [...quiz.querySelectorAll(".question")];
  const checkBtn = document.getElementById("check-btn");
  const resetBtn = document.getElementById("reset-btn");
  const score = document.getElementById("score");
  const kpiScore = document.getElementById("kpi-score");
  const scoreStyles = {
    great: "bg-teal-100 text-teal-800 dark:bg-teal-500/10 dark:text-teal-400",
    ok: "bg-amber-100 text-amber-800 dark:bg-amber-500/10 dark:text-amber-400",
    low: "bg-red-100 text-red-800 dark:bg-red-500/10 dark:text-red-400",
  };

  checkBtn.addEventListener("click", () => {
    let good = 0;
    questions.forEach((q) => {
      const correct = Number(q.dataset.correct);
      const chosen = q.querySelector("input:checked");
      q.querySelectorAll(".option").forEach((opt, i) => {
        opt.classList.toggle("is-correct", i === correct);
        opt.classList.toggle("is-wrong", chosen !== null && Number(chosen.value) === i && i !== correct);
        opt.querySelector("input").disabled = true;
      });
      if (chosen && Number(chosen.value) === correct) good++;
      q.querySelector(".explanation").hidden = false;
    });

    const pct = Math.round((good / questions.length) * 100);
    const level = pct >= 80 ? "great" : pct >= 50 ? "ok" : "low";
    score.textContent = `${good} / ${questions.length} · ${pct}%`;
    score.className = `ms-auto py-1 px-3 rounded-full text-sm font-semibold ${scoreStyles[level]}`;
    score.hidden = false;
    if (kpiScore) kpiScore.textContent = `${pct}%`;
    checkBtn.hidden = true;
    resetBtn.hidden = false;
  });

  resetBtn.addEventListener("click", () => {
    questions.forEach((q) => {
      q.querySelectorAll(".option").forEach((opt) => {
        opt.classList.remove("is-correct", "is-wrong");
        const input = opt.querySelector("input");
        input.disabled = false;
        input.checked = false;
      });
      q.querySelector(".explanation").hidden = true;
    });
    score.hidden = true;
    if (kpiScore) kpiScore.textContent = "–";
    checkBtn.hidden = false;
    resetBtn.hidden = true;
  });

  // Export
  const download = (content, name, type) => {
    const url = URL.createObjectURL(new Blob([content], { type }));
    const a = Object.assign(document.createElement("a"), { href: url, download: name });
    a.click();
    URL.revokeObjectURL(url);
  };

  quiz.querySelectorAll("[data-export]").forEach((btn) =>
    btn.addEventListener("click", () => {
      if (btn.dataset.export === "md") {
        download(document.getElementById("quiz-md").value, "quiz.md", "text/markdown");
      } else {
        const json = JSON.stringify(JSON.parse(document.getElementById("quiz-json").textContent), null, 2);
        download(json, "quiz.json", "application/json");
      }
    })
  );
})();
