# Anagram solver

The file `AnagramSolver.cs` is the source code of an exhaustive, signature-based, multi-word anagram solver. Basically, it's very smol, and as such also very inefficient memory-wise. It currently operates within the `[a-z]` range, regardless of the given dictionary, for simplicity.

**Usage**. As a console application, the solver expects a dictionary file path as its first and only command-line argument. Once an internal index has been built, you can start feeding the solver through the standard input with phrases to be anagrammed. In order to prune the search space a bit, the solving function can be parametrized by the following:

* Minimum length of each word making up an anagram;
* Maximum number of words making up an anagram.

However, these are only intended to be accessible through client code, for the time being.

# Italian dictionary / Lista di parole italiane

The file `dictionary.txt` is a list of Italian words obtained from four different sources:
- [A previous private effort][1].
- [An independent fork of the official Italian dictionary for spell-checking in LibreOffice][2], LaTeX and others, based on Hunspell; the unravelling was achieved by simply running Hunspell's `unmunch` command on the provided `.dic` and `.aff` files.
- [The Italian version of the Wiktionary][3].
- The digital version of a renowned Italian vocabulary (c'mon, you know which).

[1]: https://github.com/napolux/paroleitaliane
[2]: https://github.com/flodolo/dizionario-it
[3]: https://it.wiktionary.org

**Note**. You should be aware that `dictionary.txt` and related files may be subject to the licensing conditions imposed on the above sources by their respective authors; you can freely use and modify the rest of the source code.

**Example**. Retrieving a list of words contained in a given `category` of the Italian Wiktionary in .NET:

```csharp
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;
using WikiClientLibrary.Generators;

// ...

string category; // e.g. "Categoria:Verbi_in_italiano"

var client = new WikiClient();
var anchor = new WikiSite(client, "https://it.wiktionary.org/w/api.php");
await anchor.Initialization;

var generator = new CategoryMembersGenerator(anchor, category);
await foreach (var stub in generator.EnumItemsAsync()) {
    Console.WriteLine(stub.Title);
}
```

See also [WikiClientLibrary](https://github.com/CXuesong/WikiClientLibrary).

Let **`U`** be the set union of the above sources; the file `dictionary.txt` hereinafter referred to just as **`D`** was obtained by repeatedly applying `closure.aff` on **`U`** until fix-point.

The file `closure.aff` is a Hunspell affix file that lists some of the most productive rules in Italian, used for generating words from established ones. Vocabularies don't always report these words, deemed as either too recent and volatile, or just too numerous. Even if grammatically acceptable, the generated words can still be rejected due to a number of constraints such as phonological or suffixal, not all of them being entirely predictable. Some of these words may even be unacceptable due to the occasional need of adding an interfix, doubling a consonant, removing a diphthong, etc. See also M. Grossmann, F. Rainer; *La formazione delle Parole in Italiano* (2004).

The rules listed in `closure.aff` are intentionally over-productive: the result of each previous application of the entry-point rule `^` to all words of **`U`** is checked against MS Word's spell-checker (should be equivalent to MS Windows' spell-checking API) before the next application.

**Example**. Checking a given Italian `word` against MS Word's spell-checker in .NET:

```csharp
using Microsoft.Office.Interop.Word;

// ...

string word; // e.g. "ciao"

var app = new ApplicationClass();
var ita = app.Languages[WdLanguageID.wdItalian].Name;

if (!app.CheckSpelling(word, MainDictionary : ita,
                             IgnoreUppercase: false)) {
    
    // if the check fails, get some suggestions
    app.Documents.Add();
    SpellingSuggestions s = app.GetSpellingSuggestions(word, MainDictionary : ita,
                                                             IgnoreUppercase: false);
    // ...
}

app.Quit(false);
```

**Remark**. You'll also have to reference the following assemblies (version numbers might vary):

```
C:\Windows\assembly\GAC_MSIL\Microsoft.Office.Interop.Word\15.0.0.0__71e9bce111e9429c\Microsoft.Office.Interop.Word.dll
C:\Windows\assembly\GAC_MSIL\Microsoft.Vbe.Interop\15.0.0.0__71e9bce111e9429c\Microsoft.Vbe.Interop.dll
C:\Windows\assembly\GAC_MSIL\office\15.0.0.0__71e9bce111e9429c\OFFICE.DLL
```

See also [Office interop](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/interop/how-to-access-office-onterop-objects).

Notably, MS Word's suggestion algorithm appears to be functioning in terms of the Levenshtein distance, making it a valuable tool for correcting misattached affixes. In practice, however, this is a costly operation, and therefore was only performed before the very first application as an extra validation mechanism of the source dictionary **`U`**.

The output to this validation was then stripped of any words containing non-lowercase characters according to .NET's `char.IsLower`. This includes first names, multi-word expressions, compound words, abbreviations and the like, all of which are **not** found in the final result with the only exception of words ending with `'`.

Other practicalities are negligible here, I believe. The bulk of the procedure is captured by the following pseudo-code:

<pre>
<b>u</b> <- ∪ <i>all sources</i>
<b>U</b> <- <b>u</b>
<b>D</b> <- ∅
while (<b>u</b> ≠ ∅)
    <b>d</b> <- ∅
    foreach (w ∈ <b>u</b>)
        if (Word.SpellCheck(w))
            <b>d</b> <- <b>d</b> ∪ {w}
    WriteFile("closure.dic", { w/^ : w ∈ <b>d</b> })
    <b>u</b> <- Hunspell.Unmunch("closure.dic", "closure.aff") \ <b>U</b>
    <b>U</b> <- <b>U</b> ∪ <b>u</b>
    <b>D</b> <- <b>D</b> ∪ <b>d</b>
</pre>

At the end, one last iteration was performed with application of the plural `P` and truncation `T` rules to all words of **`D`**. Barring the first and last ones, a total of 5 iterations were needed to reach the fix-point, meaning that **`D`** contains generated words up to five-fold derived.

**Example**. One of the longest words of **`D`** is the 30-character-long generated word `intercompartimentalissimamente`, which can be viewed as four-fold derived as per `inter-compartimento-ale-issimo-amente` from the established word `compartimento` (the derivation `con-partire` is unproductive) although it should be noted that `intercompartimentale` was already established as a word.

The whole computation lasted some 8 days on an i5-2500K @3.30GHz and a single thread, as Office interop tends to misbehave when multi-threading is involved; see also
[this](https://social.msdn.microsoft.com/Forums/vstudio/en-US/a4775ced-fa6d-44bf-b039-5bc72188e823/is-applicationclass-thread-safe). The final result is a nice 4-million-words dictionary, which I consider an improvement over the above sources. (Note, however, that a few legitimate words from the sources may be missing due to the checking phase.)

# Contacts and further work

If you like the **`D`** and wish to contribute or just have a question, you can DM me on Discord @giofrida#2060 or leave a comment on the [discussions](https://github.com/sigmasaur/AnagramSolver/discussions/) here. Possible ways of improving the list, other than polishing, include decorating each word with the grammatical category and a pointer to the base word if derived, but I'm open to suggestions.
