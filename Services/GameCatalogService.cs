using nesimokau.lt.Models;

namespace nesimokau.lt.Services;

public sealed class GameCatalogService
{
    public IReadOnlyList<GameDefinition> Games { get; } =
    [
        new("dictation", "Žodžių diktantas", "Išgirsk, pagalvok ir parašyk be klaidų.", "Aa", "violet", "Lietuvių kalba", "5–8 kl.", "Vidutinis", GameStatus.Ready, 5),
        new("spelling", "Rašybos iššūkis", "Atpažink taisyklingai parašytą žodį.", "✓", "mint", "Lietuvių kalba", "3–8 kl.", "Lengvas", GameStatus.ComingSoon, 4),
        new("missing-letters", "Įrašyk praleistas raides", "Atkurk žodžius ir patikrink, ar pastebi jų rašybos ypatumus.", "_", "orange", "Lietuvių kalba", "2–6 kl.", "Lengvas", GameStatus.Ready, 5),
        new("word-match", "Žodžių poros", "Sujunk žodžius su jų reikšmėmis.", "↔", "blue", "Lietuvių kalba", "5–9 kl.", "Vidutinis", GameStatus.ComingSoon, 6),
        new("synonyms", "Sinonimai ir antonimai", "Atrask žodžių ryšius ir prasmes.", "≋", "pink", "Lietuvių kalba", "5–10 kl.", "Sudėtingas", GameStatus.ComingSoon, 6),
        new("sentence-builder", "Sakinio konstruktorius", "Sudėliok žodžius į prasmingą sakinį.", "Aa", "cyan", "Lietuvių kalba", "3–8 kl.", "Vidutinis", GameStatus.ComingSoon, 7),
        new("quiz", "Greitasis quiz", "Kiek teisingų atsakymų surinksi per 60 sekundžių?", "⚡", "yellow", "Lietuvių kalba", "5–10 kl.", "Sudėtingas", GameStatus.ComingSoon, 3),
        new("math-arena", "Skaičių arena", "Greitas skaičiavimas: sudėtis, atimtis, daugyba, dalyba.", "∑", "blue", "Matematika", "1–4 kl.", "Vidutinis", GameStatus.Ready, 5),
        new("math-word-problems", "Tekstiniai uždaviniai", "Skaityk sąlygą, rask sprendimą ir įrašyk atsakymą.", "?", "orange", "Matematika", "1–4 kl.", "Vidutinis", GameStatus.Ready, 8),
        new("math-geometry", "Geometrija ir matavimai", "Ilgis, plotas, tūris, perimetras – pritaikyk žinias praktiškai.", "◈", "cyan", "Matematika", "2–4 kl.", "Vidutinis", GameStatus.Ready, 7),
        new("math-patterns", "Dėsningumų detektyvas", "Atrask sekas, dėsningumus ir logines taisykles.", "∞", "pink", "Matematika", "1–4 kl.", "Vidutinis", GameStatus.Ready, 6),
        new("english", "English sprint", "Build your vocabulary one round at a time.", "Aa", "mint", "Anglų kalba", "5–8 kl.", "Vidutinis", GameStatus.ComingSoon, 6),
        new("history", "Laiko juosta", "Sujunk istorinius įvykius su jų laiku.", "◷", "orange", "Istorija", "5–10 kl.", "Vidutinis", GameStatus.ComingSoon, 7),
        new("science", "Mokslo laboratorija", "Tyrinėk pasaulį per trumpus iššūkius.", "✦", "violet", "Gamtos mokslai", "3–8 kl.", "Lengvas", GameStatus.ComingSoon, 5)
    ];
}
