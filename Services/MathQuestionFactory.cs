using nesimokau.lt.Models;

namespace nesimokau.lt.Services;

public static class MathQuestionFactory
{
    private static readonly string[] Difficulties = ["Lengvas", "Vidutinis", "Sudėtingas"];

    public static IReadOnlyList<MathQuestion> CreateAll()
    {
        var questions = new List<MathQuestion>(360);
        var id = 1;

        for (var grade = 1; grade <= 4; grade++)
        {
            foreach (var difficulty in Difficulties)
            {
                for (var i = 1; i <= 10; i++)
                {
                    var points = PointsForDifficulty(difficulty);
                    questions.Add(CreateWordProblem(id++, grade, difficulty, i, points));
                    questions.Add(CreateGeometryQuestion(id++, grade, difficulty, i, points));
                    questions.Add(CreatePatternQuestion(id++, grade, difficulty, i, points));
                }
            }
        }

        return questions;
    }

    private static MathQuestion CreateWordProblem(int id, int grade, string difficulty, int questionNumber, int points)
    {
        var baseValue = grade switch
        {
            1 => 6,
            2 => 12,
            3 => 24,
            _ => 40
        };

        var first = baseValue + questionNumber * 2;
        var second = grade + 2 + questionNumber % 4;

        var variant = questionNumber % 5;
        return variant switch
        {
            0 => CreateAdditionStory(id, grade, difficulty, points, first, second),
            1 => CreateSubtractionStory(id, grade, difficulty, points, first + second, second),
            2 => CreateMultiplicationStory(id, grade, difficulty, points, grade + 2, second),
            3 => CreateDivisionStory(id, grade, difficulty, points, grade + 2, second),
            _ => CreateTwoStepStory(id, grade, difficulty, points, first, second)
        };
    }

    private static MathQuestion CreateAdditionStory(int id, int grade, string difficulty, int points, int first, int second)
    {
        var answer = first + second;
        return new MathQuestion(id, "Tekstiniai uždaviniai", "Skaičiai ir skaičiavimai", $"Ieva surinko {first} kaštonus, o Tomas – {second} kaštonus. Kiek kaštonų jie surinko kartu?", "Sudėk abiejų vaikų surinktus kaštonus.", answer.ToString(), [answer.ToString()], $"{first} + {second} = {answer}.", difficulty, grade, points);
    }

    private static MathQuestion CreateSubtractionStory(int id, int grade, string difficulty, int points, int total, int removed)
    {
        var answer = total - removed;
        return new MathQuestion(id, "Tekstiniai uždaviniai", "Skaičiai ir skaičiavimai", $"Bibliotekoje buvo {total} naujų knygų. Mokiniai pasiėmė {removed} knygas. Kiek knygų liko?", "Iš visų knygų atimk paimtas.", answer.ToString(), [answer.ToString()], $"{total} - {removed} = {answer}.", difficulty, grade, points);
    }

    private static MathQuestion CreateMultiplicationStory(int id, int grade, string difficulty, int points, int groups, int each)
    {
        var answer = groups * each;
        return new MathQuestion(id, "Tekstiniai uždaviniai", "Skaičiai ir skaičiavimai", $"Į {groups} dėžutes sudėta po {each} pieštukus. Kiek pieštukų yra visose dėžutėse?", "Vienodų grupių skaičių padaugink iš kiekio vienoje grupėje.", answer.ToString(), [answer.ToString()], $"{groups} × {each} = {answer}.", difficulty, grade, points);
    }

    private static MathQuestion CreateDivisionStory(int id, int grade, string difficulty, int points, int children, int each)
    {
        var total = children * each;
        return new MathQuestion(id, "Tekstiniai uždaviniai", "Skaičiai ir skaičiavimai", $"{total} lipdukų reikia po lygiai padalyti {children} vaikams. Kiek lipdukų gaus kiekvienas vaikas?", "Visą kiekį padalyk vaikų skaičiui.", each.ToString(), [each.ToString()], $"{total} ÷ {children} = {each}.", difficulty, grade, points);
    }

    private static MathQuestion CreateTwoStepStory(int id, int grade, string difficulty, int points, int first, int second)
    {
        var added = grade + 3;
        var answer = first - second + added;
        return new MathQuestion(id, "Tekstiniai uždaviniai", "Skaičiai ir skaičiavimai", "Autobuse važiavo " + first + " keleiviai. Stotelėje išlipo " + second + ", o įlipo " + added + ". Kiek keleivių dabar važiuoja autobusu?", "Pirmiausia atimk išlipusius, po to pridėk įlipusius.", answer.ToString(), [answer.ToString()], $"{first} - {second} + {added} = {answer}.", difficulty, grade, points);
    }

    private static MathQuestion CreateGeometryQuestion(int id, int grade, string difficulty, int questionNumber, int points)
    {
        var length = 4 + grade + questionNumber % 5;
        var width = 2 + questionNumber % 4;
        var side = 3 + grade + questionNumber % 4;

        var variant = questionNumber % 5;
        return variant switch
        {
            0 => new MathQuestion(id, "Stačiakampio plotas", "Geometrija ir matavimai", $"Gėlyno ilgis {length} m, plotis {width} m. Koks jo plotas?", "Plotas = ilgis × plotis.", (length * width).ToString(), [(length * width).ToString()], $"{length} × {width} = {length * width} m².", difficulty, grade, points),
            1 => new MathQuestion(id, "Stačiakampio perimetras", "Geometrija ir matavimai", $"Stačiakampio kraštinės yra {length} cm ir {width} cm. Koks jo perimetras?", "Sudėk visas keturias kraštines.", (2 * (length + width)).ToString(), [(2 * (length + width)).ToString()], $"2 × ({length} + {width}) = {2 * (length + width)} cm.", difficulty, grade, points),
            2 => new MathQuestion(id, "Kvadrato perimetras", "Geometrija ir matavimai", $"Kvadrato kraštinė yra {side} cm. Koks jo perimetras?", "Kvadratas turi keturias vienodas kraštines.", (4 * side).ToString(), [(4 * side).ToString()], $"4 × {side} = {4 * side} cm.", difficulty, grade, points),
            3 => new MathQuestion(id, "Ilgio vienetai", "Geometrija ir matavimai", $"Juosta yra {length} m ilgio. Kiek tai yra centimetrų?", "Vienas metras yra 100 centimetrų.", (length * 100).ToString(), [(length * 100).ToString()], $"{length} m = {length * 100} cm.", difficulty, grade, points),
            _ => new MathQuestion(id, "Trūkstama kraštinė", "Geometrija ir matavimai", $"Stačiakampio perimetras yra {2 * (length + width)} cm, o ilgis – {length} cm. Koks jo plotis?", "Iš perimetro atimk abiejų ilgių sumą ir padalyk iš 2.", width.ToString(), [width.ToString()], $"Plotis yra {width} cm.", difficulty, grade, points)
        };
    }

    private static MathQuestion CreatePatternQuestion(int id, int grade, string difficulty, int questionNumber, int points)
    {
        var start = grade + questionNumber;
        var step = 2 + questionNumber % 4;

        var variant = questionNumber % 5;
        return variant switch
        {
            0 => new MathQuestion(id, "Didėjanti seka", "Modeliai ir sąryšiai", $"Įrašyk kitą sekos skaičių: {start}, {start + step}, {start + 2 * step}, {start + 3 * step}, ...", "Kiekvieną kartą pridedamas tas pats skaičius.", (start + 4 * step).ToString(), [(start + 4 * step).ToString()], $"Kiekvieną kartą pridedama po {step}.", difficulty, grade, points),
            1 => new MathQuestion(id, "Mažėjanti seka", "Modeliai ir sąryšiai", $"Įrašyk kitą sekos skaičių: {start + 5 * step}, {start + 4 * step}, {start + 3 * step}, {start + 2 * step}, ...", "Kiekvieną kartą atimamas tas pats skaičius.", (start + step).ToString(), [(start + step).ToString()], $"Kiekvieną kartą atimama po {step}.", difficulty, grade, points),
            2 => CreateDoublingPattern(id, grade, difficulty, questionNumber, points),
            3 => CreateAlternatingPattern(id, grade, difficulty, questionNumber, points),
            _ => CreateOperationPattern(id, grade, difficulty, questionNumber, points)
        };
    }

    private static MathQuestion CreateDoublingPattern(int id, int grade, string difficulty, int questionNumber, int points)
    {
        var start = 2 + grade + questionNumber % 3;
        var answer = start * 16;
        return new MathQuestion(id, "Dvigubėjanti seka", "Modeliai ir sąryšiai", $"Įrašyk kitą sekos skaičių: {start}, {start * 2}, {start * 4}, {start * 8}, ...", "Kiekvienas skaičius yra dvigubas už ankstesnį.", answer.ToString(), [answer.ToString()], $"{start * 8} × 2 = {answer}.", difficulty, grade, points);
    }

    private static MathQuestion CreateAlternatingPattern(int id, int grade, string difficulty, int questionNumber, int points)
    {
        var first = grade + questionNumber;
        var second = first + 3;
        return new MathQuestion(id, "Pasikartojanti seka", "Modeliai ir sąryšiai", $"Įrašyk kitą sekos skaičių: {first}, {second}, {first}, {second}, {first}, ...", "Seka kartoja du skaičius.", second.ToString(), [second.ToString()], $"Kartojasi {first}, {second}.", difficulty, grade, points);
    }

    private static MathQuestion CreateOperationPattern(int id, int grade, string difficulty, int questionNumber, int points)
    {
        var start = 3 + grade + questionNumber;
        var answer = start + 12;
        return new MathQuestion(id, "Veiksmų seka", "Modeliai ir sąryšiai", $"Įrašyk kitą sekos skaičių: {start}, {start + 3}, {start + 6}, {start + 9}, ...", "Kiekvieną kartą pridedama po 3.", answer.ToString(), [answer.ToString()], $"{start + 9} + 3 = {answer}.", difficulty, grade, points);
    }

    private static int PointsForDifficulty(string difficulty)
        => difficulty switch
        {
            "Lengvas" => 50,
            "Vidutinis" => 75,
            _ => 100
        };
}
