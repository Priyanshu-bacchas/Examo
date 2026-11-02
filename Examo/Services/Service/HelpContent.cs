using System.Text.RegularExpressions;

using Examo.DTOs.Support;

namespace Examo.Services;

// Help section ka sara content (FAQ) aur search logic.
// Naya sawal-jawab add karna ho to neeche Entries list me ek line jodo.
public static class HelpContent
{
    private sealed class Entry
    {
        public int Id { get; init; }
        public string Category { get; init; } = "";
        public string Question { get; init; } = "";
        public string Answer { get; init; } = "";
        public string Keywords { get; init; } = "";

        // search index (startup par ek baar bante hain)
        public HashSet<string> QuestionTokens { get; set; } = new();
        public HashSet<string> KeywordTokens { get; set; } = new();
        public HashSet<string> AnswerTokens { get; set; } = new();
        public string QuestionNorm { get; set; } = "";
    }

    // =====================================================
    // FAQ ENTRIES
    // =====================================================

    private static readonly List<Entry> Entries = new()
    {
        new Entry
        {
            Id = 1,
            Category = "Student Basics",
            Question = "How to use this website?",
            Answer = "Examo helps you track government exam forms, deadlines, preparation and results in one place. 1) Sign up, verify your email and log in. 2) Open Jobs (sidebar > Opportunities) and look at Latest Jobs. 3) Click 'Add to ExamForm' on the exam you want to apply for. It is added to Exam Forms, Exams, Preparation and Results automatically. 4) Open Student > Exam Forms, click 'Open' to fill the form on the official website, then change its status from Pending to Filled. 5) Use Student > Preparation, Subjects and Schedule to plan your study, and Documents to keep your files. 6) After the exam, update your result in Student > Results. 7) Notifications remind you before every last date and exam date.",
            Keywords = "use website how to use guide tutorial steps start begin process flow work working works overview basics app examo explain kaise kaam istemal chalaye shuru"
        },
        new Entry
        {
            Id = 2,
            Category = "General",
            Question = "What is Examo?",
            Answer = "Examo is a student management app for government exam aspirants. You can find the latest jobs, add them to your own Exam Forms, track exam dates, preparation, results, subjects, study schedule and documents, and get reminders before important dates.",
            Keywords = "what is examo about app website purpose features aspirants government exam sarkari naukri kya hai"
        },
        new Entry
        {
            Id = 3,
            Category = "Student Basics",
            Question = "How to set up my student profile?",
            Answer = "Sign up, verify your email with the 6-digit code and you will land on your Dashboard. Your name, email and mobile number come from your signup details, so there is nothing else to set up. Everything you add or track is visible only to your account.",
            Keywords = "profile setup account start begin dashboard name details register signup new"
        },
        new Entry
        {
            Id = 4,
            Category = "Student Basics",
            Question = "What does the Dashboard show?",
            Answer = "Your Dashboard gives a quick summary of Upcoming Exams, Subjects, Study Sessions and Exam Forms, along with a calendar so you can see your important dates at a glance.",
            Keywords = "dashboard home summary overview calendar upcoming exams study sessions count cards main page"
        },
        new Entry
        {
            Id = 5,
            Category = "Student Basics",
            Question = "How to add subjects and schedules?",
            Answer = "Subjects: go to Student > Subjects, click Add, then enter the subject name, status (Not Started, In Progress or Completed), number of lectures, notes/materials, a link and an optional PDF. Schedules: go to Student > Schedule, choose the subject, date and start time (end time is optional) and add a short description.",
            Keywords = "subject subjects schedule schedules timetable lecture lectures study plan add create syllabus pdf link"
        },
        new Entry
        {
            Id = 6,
            Category = "Student Basics",
            Question = "How to upload my documents?",
            Answer = "Go to Student > Documents and upload your file. You can upload photos (JPG, PNG, WEBP) and PDF files up to 10 MB each. Give the document a name so it is easy to find later. Your documents are private to your account.",
            Keywords = "document documents upload photo pdf file certificate marksheet image scan store save"
        },
        new Entry
        {
            Id = 7,
            Category = "Exam Enrollment",
            Question = "How do I add an exam form?",
            Answer = "Exam forms are added from the Jobs page, not typed in by hand. 1) Go to Jobs (sidebar > Opportunities) and open the Latest Jobs tab. 2) Click 'Watch' under Details if you want to see a video about the job. 3) Click 'Add to ExamForm' next to the exam. The exam is added to Student > Exam Forms (Pending), Exams (Coming Soon), Preparation (Not Started) and Results (Awaited) in one go, and the button changes to 'Added'.",
            Keywords = "add exam form examform job jobs apply application new latest jobs button create fill bharna form kaise add to examform naukri vacancy"
        },
        new Entry
        {
            Id = 8,
            Category = "Exam Enrollment",
            Question = "How do I change my exam form status?",
            Answer = "Go to Student > Exam Forms. Click 'Open' in the Link column to fill the form on the official website. After you have submitted it, click Edit on that row and change the status from Pending to Filled. Only the status can be changed; the exam name and dates come from the job.",
            Keywords = "change update status exam form filled pending edit mark submitted fill link open official website form status badle badlo"
        },
        new Entry
        {
            Id = 9,
            Category = "Exam Enrollment",
            Question = "Can I add an exam form manually?",
            Answer = "No. To keep your data correct, Exam Forms, Exams, Preparation and Results are created only from the Jobs page. If an exam you want is not listed in Jobs yet, please check again later or tell us through Contact Us and we will look into it.",
            Keywords = "manually manual add own type enter create custom missing listed available job found exam not in jobs hand"
        },
        new Entry
        {
            Id = 10,
            Category = "Exam Enrollment",
            Question = "Why can't I add a job to my Exam Forms?",
            Answer = "The 'Add to ExamForm' button works only when the last date of the job has not passed, your free trial or paid plan is active, and you have not already added it (then it shows 'Added'). If your plan has ended, open Subscription and choose a plan.",
            Keywords = "cannot cant unable button disabled added last date passed expired plan trial error not working greyed"
        },
        new Entry
        {
            Id = 11,
            Category = "Exam Enrollment",
            Question = "What are the Jobs, Admit Card and Results tabs?",
            Answer = "The Jobs page has three tabs. Latest Jobs lists new notifications with the start date, last date and an 'Add to ExamForm' button. Admit Card lists the latest admit cards and Results lists the latest declared results; use 'Open' to go to the official page and 'Watch' for a video when available. A tag like '1 day left' warns you when a last date is near.",
            Keywords = "jobs tab tabs latest admit card hall ticket result open watch video details last date left opportunities"
        },
        new Entry
        {
            Id = 12,
            Category = "Exam Enrollment",
            Question = "Where can I track my exam dates?",
            Answer = "Open Student > Exams to see every exam you added with its date and status (Coming Soon or Done). You can change the status with Edit. Use Student > Schedule to plan your daily study slots and your Dashboard calendar to see dates at a glance.",
            Keywords = "exam exams date dates track calendar upcoming when coming soon done timetable"
        },
        new Entry
        {
            Id = 13,
            Category = "Exam Enrollment",
            Question = "How do I track my exam preparation?",
            Answer = "Exams you add from Jobs appear in Student > Preparation automatically with their syllabus. Set the status to Not Started, In Progress or Completed with Edit as you move ahead.",
            Keywords = "preparation prepare syllabus progress status study track exam not started in progress completed"
        },
        new Entry
        {
            Id = 14,
            Category = "Exam Enrollment",
            Question = "How to check my results?",
            Answer = "Student > Results lists every exam you added and shows Awaited until the result is declared on the official website. After that, click Edit and set it to Qualified or Not Qualified. You can also see the latest declared results in Jobs > Results.",
            Keywords = "result results qualified not qualified awaited declared score rank marks check status"
        },
        new Entry
        {
            Id = 15,
            Category = "General",
            Question = "What are Notifications and reminders?",
            Answer = "Notifications remind you about your Pending exam forms (a week before, a day before and on the last date) and about your upcoming exams (a week before, a day before and on the exam day). Once you mark a form as Filled, its reminders stop. Open Notifications in the sidebar to read or delete them.",
            Keywords = "notification notifications reminder reminders alert alerts remind last date email bell"
        },
        new Entry
        {
            Id = 16,
            Category = "Subscription",
            Question = "How does the free trial work?",
            Answer = "New users get a free trial with full access. When the trial ends, only the Help and Subscription pages stay open until you choose a plan. You can see your trial end date on the Subscription page.",
            Keywords = "free trial basic plan days start new user expired ended locked access trial kitne din"
        },
        new Entry
        {
            Id = 17,
            Category = "Subscription",
            Question = "How much does Examo cost?",
            Answer = "Examo has a free trial, then a Monthly plan and a Yearly plan. The current prices are always shown on the Subscription page.",
            Keywords = "cost price pricing fee charges how much rate monthly yearly plan kitna paisa"
        },
        new Entry
        {
            Id = 18,
            Category = "Subscription",
            Question = "What payment methods are accepted?",
            Answer = "We accept UPI, debit cards, credit cards and net banking for all paid plans. Payments are processed securely by PayU.",
            Keywords = "payment pay upi card debit credit net banking methods payu secure"
        },
        new Entry
        {
            Id = 19,
            Category = "Subscription",
            Question = "How do I buy or renew a plan?",
            Answer = "Open Subscription from the sidebar and click the Monthly or Yearly plan. You will go to the secure PayU page and, after paying, you are brought back to Examo with your plan active. Plans are one-time payments and do not renew automatically. If you buy again while a plan is running, the new days are added after the current end date.",
            Keywords = "buy purchase renew extend upgrade plan subscription how pay monthly yearly activate payment"
        },
        new Entry
        {
            Id = 20,
            Category = "Subscription",
            Question = "How can I change or cancel my plan?",
            Answer = "There is no auto-renewal, so nothing needs to be cancelled: your plan simply ends on its end date. To continue, buy a plan again from the Subscription page.",
            Keywords = "plan change cancel subscription monthly yearly basic downgrade stop auto renew refund"
        },
        new Entry
        {
            Id = 21,
            Category = "Subscription",
            Question = "How do I check when my plan ends?",
            Answer = "Open Subscription. Under Your Subscription Details you can see your status, your plan and the 'Valid Till' date (or the trial end date if you are on the free trial).",
            Keywords = "plan ends end date valid till expiry expire when status active check validity days left"
        },
        new Entry
        {
            Id = 22,
            Category = "Subscription",
            Question = "How do I download my payment receipt?",
            Answer = "Go to Subscription > Previous Payments and click Download next to the payment. You get a colourful payment slip image (PNG) with the amount, plan start date, plan end date, payment ID and transaction ID.",
            Keywords = "receipt invoice payment download bill previous payments slip proof"
        },
        new Entry
        {
            Id = 23,
            Category = "Subscription",
            Question = "My payment was deducted but my plan is not active",
            Answer = "Please wait a few minutes and refresh the Subscription page, because confirmation can take a short while. If your plan is still not active, send us a message through Contact Us with your registered email, the payment ID or transaction ID and the date of payment.",
            Keywords = "payment deducted money cut debited plan not active failed pending paid but not working stuck refund problem"
        },
        new Entry
        {
            Id = 24,
            Category = "Technical Issues",
            Question = "I can't log in to my account",
            Answer = "Please check these in order: 1) Use the same email (or mobile number) and password you signed up with. 2) After the password, we email a 6-digit login code. Check your Spam/Promotions folder, and use 'Resend code' after 60 seconds if needed. 3) If your email was never verified, we send a verification code instead. 4) Forgot your password? Click 'Forgot Password?' on the login page. 5) You can also use 'Continue with Google' if you signed up with the same Google email. Still stuck? Please contact our support team with your registered email.",
            Keywords = "login log signin cant cannot unable access account password wrong invalid incorrect locked problem"
        },
        new Entry
        {
            Id = 25,
            Category = "Account & Login",
            Question = "How to reset my password?",
            Answer = "On the login page click 'Forgot Password?', enter your registered email and we will send a 6-digit code. Enter the code, choose a new password and log in with it. The code is valid for 10 minutes.",
            Keywords = "reset forgot forget password change recover new pasword reset code email"
        },
        new Entry
        {
            Id = 26,
            Category = "Account & Login",
            Question = "I did not receive the OTP / verification code",
            Answer = "Check your Spam and Promotions folders first and make sure the email address is spelled correctly. Wait 60 seconds and press 'Resend code'. Only the latest code works, it expires in 10 minutes, and after 5 wrong attempts you need to request a new one. If nothing arrives after a couple of tries, please contact our support team.",
            Keywords = "otp code verification verify email not received receive coming spam resend mail no mail gmail"
        },
        new Entry
        {
            Id = 27,
            Category = "Account & Login",
            Question = "Why do I get a code every time I log in?",
            Answer = "To keep your account safe, we confirm each login with a one-time code sent to your email, so nobody can get in with only your password. Logging in with 'Continue with Google' does not need this extra code.",
            Keywords = "otp every time login code security two step 2fa extra verification why always"
        },
        new Entry
        {
            Id = 28,
            Category = "Account & Login",
            Question = "How does Continue with Google work?",
            Answer = "Click 'Continue with Google' on the login page and pick your Google account. If you are new, your Examo account is created automatically; if you already have one with the same email, you are simply logged in. Make sure your browser allows popups for this site.",
            Keywords = "google gmail continue social login popup account sign in one click"
        },
        new Entry
        {
            Id = 29,
            Category = "Technical Issues",
            Question = "My document upload is failing",
            Answer = "Please check that: the file is a JPG, PNG, WEBP or PDF; it is 10 MB or smaller; and it is a real file of that type (renamed files are rejected). Also check your internet connection and try again. If it still fails, please contact our support team with the file type and size.",
            Keywords = "document upload failing fail failed error size large big pdf photo file type not uploading problem"
        },
        new Entry
        {
            Id = 30,
            Category = "Technical Issues",
            Question = "The page is not loading properly",
            Answer = "If the site has not been used for a while, the first load can take up to a minute because the server needs to wake up. Wait a moment and refresh. You can also try a hard refresh (Ctrl + Shift + R), another browser, or check your internet connection. If you see 'Failed to fetch', wait a minute and try again. If the problem continues, please contact our support team with a screenshot.",
            Keywords = "page loading load slow blank white screen not working fetch error stuck refresh browser open website down"
        },
        new Entry
        {
            Id = 31,
            Category = "Account & Login",
            Question = "Is my data and uploaded documents safe?",
            Answer = "Yes. Your exam forms, exams, subjects, schedules and documents are visible only to your account, and every login is protected with an email code.",
            Keywords = "data safe security privacy private secure documents protected visible"
        },
        new Entry
        {
            Id = 32,
            Category = "Account & Login",
            Question = "How do I change my email or mobile number?",
            Answer = "You cannot change these yourself yet. Please contact our support team with your registered email and the new details.",
            Keywords = "change email mobile phone number update edit contact details new number"
        },
        new Entry
        {
            Id = 33,
            Category = "Account & Login",
            Question = "How do I delete my account?",
            Answer = "Please contact our support team using your registered email and ask for account deletion.",
            Keywords = "delete remove account data close deactivate erase"
        },
        new Entry
        {
            Id = 34,
            Category = "Account & Login",
            Question = "My account is blocked",
            Answer = "An account can be blocked by an administrator. Please contact our support team with your registered email.",
            Keywords = "blocked block suspended banned disabled account access denied cannot login"
        },
        new Entry
        {
            Id = 35,
            Category = "General",
            Question = "How do I contact support?",
            Answer = "Use the Contact Us form on the Help page (or ask the Examo Assistant first) and mention your registered email address so that we can find your account. You can also email us at examoaspirant@gmail.com.",
            Keywords = "contact support help query question complaint feedback team reply ticket message email talk human"
        }
    };

    // Featured cards (frontend ke 3 card, links upar ke questions hain)
    private static readonly List<FeaturedGroupDto> Featured = new()
    {
        new FeaturedGroupDto
        {
            Title = "Student Basics",
            Tone = "mint",
            Links = new()
            {
                "How to use this website?",
                "What does the Dashboard show?",
                "How to upload my documents?"
            }
        },
        new FeaturedGroupDto
        {
            Title = "Exam Enrollment",
            Tone = "lavender",
            Links = new()
            {
                "How do I add an exam form?",
                "How do I change my exam form status?",
                "How to check my results?"
            }
        },
        new FeaturedGroupDto
        {
            Title = "Technical Issues",
            Tone = "sky",
            Links = new()
            {
                "I can't log in to my account",
                "My document upload is failing",
                "The page is not loading properly"
            }
        }
    };

    // =====================================================
    // SEARCH INDEX
    // =====================================================

    private static readonly Regex NonWord =
        new(@"[^a-z0-9]+", RegexOptions.Compiled);

    // Ye shabd search me ginte nahi (English + Hinglish)
    private static readonly HashSet<string> Stop = new()
    {
        "the","an","is","are","am","was","to","of","in","on","for","and","or",
        "my","me","we","you","it","this","that","how","what","why","when",
        "where","can","do","does","did","not","with","from","at","be","have",
        "has","get","want","need","please","help","there","your","our","any",
        "kya","kaise","kaisa","kyu","kyun","kab","kahan","hai","hain","ho",
        "hota","hoti","mera","meri","mere","mujhe","ka","ki","ke","ko","me",
        "mein","se","par","pe","nahi","nhi","aur","bhi","ek","karna","kare",
        "karu","kru","krna","sakta","sakte","skta","rha","raha","rahi"
    };

    // Milte-julte shabd ek hi shabd me (spelling / Hinglish / synonyms)
    private static readonly Dictionary<string, string> Synonyms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["pasword"] = "password",
            ["paswd"] = "password",
            ["passwd"] = "password",
            ["pwd"] = "password",
            ["passcode"] = "password",

            ["forgot"] = "reset",
            ["forget"] = "reset",
            ["forgotten"] = "reset",
            ["recover"] = "reset",
            ["recovery"] = "reset",

            ["verification"] = "otp",
            ["verify"] = "otp",
            ["verified"] = "otp",
            ["verifying"] = "otp",
            ["code"] = "otp",
            ["codes"] = "otp",

            ["signin"] = "login",
            ["signing"] = "login",
            ["sign"] = "login",
            ["logon"] = "login",
            ["log"] = "login",
            ["logging"] = "login",
            ["logged"] = "login",

            ["attach"] = "upload",
            ["attachment"] = "upload",
            ["attaching"] = "upload",

            ["doc"] = "document",
            ["docs"] = "document",

            ["pic"] = "photo",
            ["pics"] = "photo",
            ["picture"] = "photo",
            ["pictures"] = "photo",
            ["image"] = "photo",
            ["images"] = "photo",
            ["jpg"] = "photo",
            ["jpeg"] = "photo",
            ["png"] = "photo",

            ["rizalt"] = "result",

            ["pay"] = "payment",
            ["paid"] = "payment",
            ["paying"] = "payment",
            ["upi"] = "payment",
            ["billing"] = "payment",
            ["bill"] = "payment",
            ["receipt"] = "payment",
            ["invoice"] = "payment",
            ["transaction"] = "payment",
            ["paisa"] = "payment",

            ["subscription"] = "plan",
            ["subscribe"] = "plan",
            ["premium"] = "plan",
            ["monthly"] = "plan",
            ["yearly"] = "plan",
            ["price"] = "plan",
            ["pricing"] = "plan",
            ["upgrade"] = "plan",

            ["cancellation"] = "cancel",

            ["remove"] = "delete",
            ["deactivate"] = "delete",
            ["erase"] = "delete",

            ["mail"] = "email",
            ["phone"] = "mobile",
            ["number"] = "mobile",

            ["blocked"] = "block",
            ["blocking"] = "block",
            ["suspended"] = "block",
            ["suspend"] = "block",
            ["banned"] = "block",
            ["ban"] = "block",

            ["slow"] = "load",
            ["hang"] = "load",
            ["hanging"] = "load",
            ["stuck"] = "load",
            ["freeze"] = "load",
            ["freezing"] = "load",
            ["blank"] = "load",
            ["crash"] = "load",

            ["error"] = "problem",
            ["errors"] = "problem",
            ["bug"] = "problem",
            ["fail"] = "problem",
            ["failed"] = "problem",
            ["failing"] = "problem",
            ["fails"] = "problem",
            ["broken"] = "problem",
            ["problems"] = "problem",
            ["issue"] = "problem",
            ["issues"] = "problem"
        };

    static HelpContent()
    {
        foreach (var e in Entries)
        {
            e.QuestionTokens = new HashSet<string>(Tokens(e.Question));
            e.KeywordTokens = new HashSet<string>(Tokens(e.Keywords));
            e.AnswerTokens = new HashSet<string>(Tokens(e.Answer));
            e.QuestionNorm = string.Join(" ", Tokens(e.Question));
        }
    }

    private static string Stem(string w)
    {
        if (w.Length > 5 && w.EndsWith("ing")) return w[..^3];
        if (w.Length > 4 && w.EndsWith("ed")) return w[..^2];
        if (w.Length > 3 && w.EndsWith("s") && !w.EndsWith("ss")) return w[..^1];
        return w;
    }

    private static List<string> Tokens(string? text)
    {
        var list = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
            return list;

        foreach (var raw in NonWord.Split(text.ToLowerInvariant()))
        {
            if (raw.Length < 2 || Stop.Contains(raw))
                continue;

            var token = Synonyms.TryGetValue(raw, out var mapped)
                ? mapped
                : Stem(raw);

            if (token.Length < 2 || Stop.Contains(token))
                continue;

            list.Add(token);
        }

        return list;
    }

    private static FaqDto ToDto(Entry e) => new()
    {
        Id = e.Id,
        Category = e.Category,
        Question = e.Question,
        Answer = e.Answer
    };

    // =====================================================
    // PUBLIC API
    // =====================================================

    public static HelpContentDto GetContent() => new()
    {
        Featured = Featured,
        Faqs = Entries.Select(ToDto).ToList()
    };

    public static List<FaqDto> Search(string? query, int take = 5)
    {
        var queryTokens = Tokens(query).Distinct().ToList();

        if (queryTokens.Count == 0)
            return new List<FaqDto>();

        var queryNorm = string.Join(" ", Tokens(query));

        var scored = new List<(Entry Entry, int Score)>();

        foreach (var e in Entries)
        {
            var score = 0;
            var strongMatches = 0;

            foreach (var t in queryTokens)
            {
                var inKeywords = e.KeywordTokens.Contains(t);
                var inQuestion = e.QuestionTokens.Contains(t);

                if (inKeywords) score += 4;
                if (inQuestion) score += 3;
                if (e.AnswerTokens.Contains(t)) score += 1;

                if (inKeywords || inQuestion) strongMatches++;
            }

            // Poora sawal milta ho to sabse upar
            if (queryNorm.Length >= 4 &&
                (e.QuestionNorm.Contains(queryNorm) ||
                 queryNorm.Contains(e.QuestionNorm)))
            {
                score += 12;
            }

            // Ek se zyada shabd match hue to bonus
            if (strongMatches >= 2)
                score += 2;

            if (score >= 4)
                scored.Add((e, score));
        }

        return scored
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Entry.Id)
            .Take(take)
            .Select(x => ToDto(x.Entry))
            .ToList();
    }
}