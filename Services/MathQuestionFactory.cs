using nesimokau.lt.Models;

namespace nesimokau.lt.Services;

public static class MathQuestionFactory
{
    private static readonly string[] Difficulties = ["Lengvas", "Vidutinis", "Sudėtingas"];

    public static IReadOnlyList<MathQuestion> CreateAll()
    {
        var questions = new List<MathQuestion>(120);
        var id = 1;

        for (var grade = 1; grade <= 4; grade++)
        {
            foreach (var difficulty in Difficulties)
            {
                for (var i = 1; i <= 10; i++)
                {
                    questions.Add(CreateQuestion(id++, grade, difficulty, i));
                }
            }
        }

        return questions;
    }

    private static MathQuestion CreateQuestion(int id, int grade, string difficulty, int index)
    {
        var categoryType = (index - 1) % 3;
        var points = PointsForDifficulty(difficulty);

        return categoryType switch
        {
            0 => CreateArithmeticQuestion(id, grade, difficulty, index, points),
            1 => CreateGeometryQuestion(id, grade, difficulty, index, points),
            _ => CreatePatternQuestion(id, grade, difficulty, index, points)
        };
    }

    private static MathQuestion CreateArithmeticQuestion(int id, int grade, string difficulty, int index, int points)
    {
        if (difficulty == "Lengvas")
        {
            if (index % 2 == 0)
            {
                var a = 5 + grade + index;
                var b = 2 + grade;
                var answer = a + b;
                return new MathQuestion(id, "Sudėtis", "Skaičiai ir skaičiavimai", $"Kiek yra {a} + {b}?", "Sudėk abu skaičius.", answer.ToString(), [answer.ToString()], $"{a} + {b} = {answer}.", difficulty, grade, points);
            }

            var a2 = 14 + grade + index;
            var b2 = 3 + grade;
            var answer2 = a2 - b2;
            return new MathQuestion(id, "Atimtis", "Skaičiai ir skaičiavimai", $"Kiek yra {a2} - {b2}?", "Iš pirmo skaičiaus atimk antrą.", answer2.ToString(), [answer2.ToString()], $"{a2} - {b2} = {answer2}.", difficulty, grade, points);
        }

        if (difficulty == "Vidutinis")
        {
            if (index % 2 == 0)
            {
                var a = 12 + grade * 4 + index;
                var b = 9 + grade * 3;
                var answer = a + b;
                return new MathQuestion(id, "Sudėtis 100 ribose", "Skaičiai ir skaičiavimai", $"Kiek yra {a} + {b}?", "Sudėk dešimtis ir vienetus.", answer.ToString(), [answer.ToString()], $"{a} + {b} = {answer}.", difficulty, grade, points);
            }

            var a2 = 70 + grade * 5 + index;
            var b2 = 18 + grade * 2;
            var answer2 = a2 - b2;
            return new MathQuestion(id, "Atimtis 100 ribose", "Skaičiai ir skaičiavimai", $"Kiek yra {a2} - {b2}?", "Atimk dešimtis, tada vienetus.", answer2.ToString(), [answer2.ToString()], $"{a2} - {b2} = {answer2}.", difficulty, grade, points);
        }

        if (index % 2 == 0)
        {
            var a = 4 + grade + index % 4;
            var b = 5 + index % 5;
            var answer = a * b;
            return new MathQuestion(id, "Daugyba", "Skaičiai ir skaičiavimai", $"Kiek yra {a} × {b}?", "Naudok daugybos lentelę.", answer.ToString(), [answer.ToString()], $"{a} × {b} = {answer}.", difficulty, grade, points);
        }

        var divisor = 3 + index % 6;
        var quotient = 4 + grade + index % 4;
        var dividend = divisor * quotient;
        return new MathQuestion(id, "Dalyba", "Skaičiai ir skaičiavimai", $"Kiek yra {dividend} ÷ {divisor}?", $"Pagalvok, kiek kartų {divisor} telpa į {dividend}.", quotient.ToString(), [quotient.ToString()], $"{dividend} ÷ {divisor} = {quotient}.", difficulty, grade, points);
    }

    private static MathQuestion CreateGeometryQuestion(int id, int grade, string difficulty, int index, int points)
    {
        if (difficulty == "Lengvas")
        {
            var length = 3 + grade + index % 5;
            var width = 2 + index % 3;
            var answer = length * width;
            return new MathQuestion(id, "Ilgis ir plotas", "Geometrija ir matavimai", $"Stačiakampio ilgis {length} cm, plotis {width} cm. Koks plotas?", "Plotas = ilgis × plotis.", answer.ToString(), [answer.ToString()], $"{length} × {width} = {answer} cm².", difficulty, grade, points);
        }

        if (difficulty == "Vidutinis")
        {
            var a = 4 + grade + index % 4;
            var b = 3 + index % 4;
            var answer = 2 * (a + b);
            return new MathQuestion(id, "Perimetras", "Geometrija ir matavimai", $"Stačiakampio kraštinės yra {a} cm ir {b} cm. Koks perimetras?", "Perimetras = 2 × (a + b).", answer.ToString(), [answer.ToString()], $"2 × ({a} + {b}) = {answer} cm.", difficulty, grade, points);
        }

        var edge = 5 + grade + index % 4;
        var area = edge * edge;
        return new MathQuestion(id, "Kvadrato plotas", "Geometrija ir matavimai", $"Kvadrato kraštinė yra {edge} cm. Koks kvadrato plotas?", "Kvadrato plotas = kraštinė × kraštinė.", area.ToString(), [area.ToString()], $"{edge} × {edge} = {area} cm².", difficulty, grade, points);
    }

    private static MathQuestion CreatePatternQuestion(int id, int grade, string difficulty, int index, int points)
    {
        if (difficulty == "Lengvas")
        {
            var start = grade + index;
            var step = 2 + index % 3;
            var answer = start + 4 * step;
            return new MathQuestion(id, "Dėsningumai", "Modeliai ir sąryšiai", $"Koks kitas skaičius sekoje: {start}, {start + step}, {start + 2 * step}, {start + 3 * step}, ...?", "Skaičiai didėja tuo pačiu skirtumu.", answer.ToString(), [answer.ToString()], $"Didėja po {step}, todėl kitas skaičius {answer}.", difficulty, grade, points);
        }

        if (difficulty == "Vidutinis")
        {
            var start = 2 + grade + index % 3;
            var answer = start * 16;
            return new MathQuestion(id, "Dvigubėjanti seka", "Modeliai ir sąryšiai", $"Koks kitas skaičius sekoje: {start}, {start * 2}, {start * 4}, {start * 8}, ...?", "Kiekvienas skaičius dvigubinamas.", answer.ToString(), [answer.ToString()], $"Po {start * 8} eina {answer}.", difficulty, grade, points);
        }

        var a = 40 + grade * 5 + index;
        var b = 30 + grade * 4 + index;
        var answer2 = Math.Max(a, b);
        return new MathQuestion(id, "Loginis mąstymas", "Modeliai ir sąryšiai", $"Kuris skaičius didesnis: {a} ar {b}?", "Palygink dešimtis, tada vienetus.", answer2.ToString(), [answer2.ToString()], $"Didesnis skaičius yra {answer2}.", difficulty, grade, points);
    }

    private static int PointsForDifficulty(string difficulty)
        => difficulty switch
        {
            "Lengvas" => 50,
            "Vidutinis" => 75,
            _ => 100
        };
}
