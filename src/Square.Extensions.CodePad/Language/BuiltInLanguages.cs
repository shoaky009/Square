namespace Square.Extensions.CodePad;

/// <summary>内置 Monarch JSON（Monaco 风格子集）。</summary>
internal static class BuiltInLanguages
{
    public const string CSharpMonarch = """
    {
      "defaultToken": "source",
      "tokenPostfix": ".cs",
      "keywords": [
        "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const","continue",
        "decimal","default","delegate","do","double","else","enum","event","explicit","extern","false","finally",
        "fixed","float","for","foreach","goto","if","implicit","in","int","interface","internal","is","lock",
        "long","namespace","new","null","object","operator","out","override","params","private","protected",
        "public","readonly","ref","return","sbyte","sealed","short","sizeof","stackalloc","static","string",
        "struct","switch","this","throw","true","try","typeof","uint","ulong","unchecked","unsafe","ushort",
        "using","virtual","void","volatile","while","record","var","async","await","nameof","when","with","init","required"
      ],
      "tokenizer": {
        "root": [
          ["\\/\\*", "comment", "@comment"],
          ["\\/\\/.*$", "comment"],
          ["\"", "string", "@string"],
          ["[a-zA-Z_]\\w*", { "cases": { "@keywords": "keyword", "@default": "identifier" } }],
          ["\\d+(\\.\\d+)?[fFdDmM]?", "number"],
          ["[{}()\\[\\]]", "delimiter.bracket"],
          ["[;,.]", "delimiter"],
          ["[<>=!&|/+\\-*%^~]+", "operator"],
          ["\\s+", "white"]
        ],
        "comment": [
          ["[^\\*]+", "comment"],
          ["\\*\\/", "comment", "@pop"],
          ["\\*", "comment"]
        ],
        "string": [
          ["[^\\\\\"]+", "string"],
          ["\\\\.", "string.escape"],
          ["\"", "string", "@pop"]
        ]
      }
    }
    """;

    public const string JavaScriptMonarch = """
    {
      "defaultToken": "source",
      "tokenPostfix": ".js",
      "keywords": [
        "break","case","catch","class","const","continue","debugger","default","delete","do","else","export",
        "extends","false","finally","for","function","if","import","in","instanceof","let","new","null",
        "return","super","switch","this","throw","true","try","typeof","var","void","while","with","yield",
        "async","await","of","static","get","set","from","as"
      ],
      "tokenizer": {
        "root": [
          ["\\/\\*", "comment", "@comment"],
          ["\\/\\/.*$", "comment"],
          ["\"", "string", "@string_double"],
          ["'", "string", "@string_single"],
          ["`", "string", "@string_backtick"],
          ["[a-zA-Z_$][\\w$]*", { "cases": { "@keywords": "keyword", "@default": "identifier" } }],
          ["\\d+(\\.\\d+)?([eE][+-]?\\d+)?", "number"],
          ["[{}()\\[\\]]", "delimiter.bracket"],
          ["[;,.]", "delimiter"],
          ["[<>=!&|/+\\-*%^~?:]+", "operator"],
          ["\\s+", "white"]
        ],
        "comment": [
          ["[^\\*]+", "comment"],
          ["\\*\\/", "comment", "@pop"],
          ["\\*", "comment"]
        ],
        "string_double": [
          ["[^\\\\\"]+", "string"],
          ["\\\\.", "string.escape"],
          ["\"", "string", "@pop"]
        ],
        "string_single": [
          ["[^\\\\']+", "string"],
          ["\\\\.", "string.escape"],
          ["'", "string", "@pop"]
        ],
        "string_backtick": [
          ["[^\\\\`$]+", "string"],
          ["\\\\.", "string.escape"],
          ["`", "string", "@pop"]
        ]
      }
    }
    """;

    public const string JsonMonarch = """
    {
      "defaultToken": "source",
      "tokenPostfix": ".json",
      "tokenizer": {
        "root": [
          ["\\s+", "white"],
          ["[{}\\[\\]]", "delimiter.bracket"],
          ["[,:]", "delimiter"],
          ["\"([^\"\\\\]|\\\\.)*\"(?=\\s*:)", "key"],
          ["\"([^\"\\\\]|\\\\.)*\"", "string"],
          ["-?\\d+(\\.\\d+)?([eE][+-]?\\d+)?", "number"],
          ["true|false|null", "keyword"]
        ]
      }
    }
    """;

    public const string PythonMonarch = """
    {
      "defaultToken": "source",
      "tokenPostfix": ".py",
      "keywords": [
        "and","as","assert","async","await","break","class","continue","def","del","elif","else","except",
        "False","finally","for","from","global","if","import","in","is","lambda","None","nonlocal","not",
        "or","pass","raise","return","True","try","while","with","yield"
      ],
      "tokenizer": {
        "root": [
          ["#.*$", "comment"],
          ["\"", "string", "@string_double"],
          ["'", "string", "@string_single"],
          ["[a-zA-Z_]\\w*", { "cases": { "@keywords": "keyword", "@default": "identifier" } }],
          ["\\d+(\\.\\d+)?", "number"],
          ["[{}()\\[\\]]", "delimiter.bracket"],
          ["[;,.]", "delimiter"],
          ["[<>=!@&|/+\\-*%^~]+", "operator"],
          ["\\s+", "white"]
        ],
        "string_double": [
          ["[^\\\\\"]+", "string"],
          ["\\\\.", "string.escape"],
          ["\"", "string", "@pop"]
        ],
        "string_single": [
          ["[^\\\\']+", "string"],
          ["\\\\.", "string.escape"],
          ["'", "string", "@pop"]
        ]
      }
    }
    """;

    public const string HtmlMonarch = """
    {
      "defaultToken": "source",
      "tokenPostfix": ".html",
      "tokenizer": {
        "root": [
          ["<!--", "comment", "@comment"],
          ["</[\\w:-]+\\s*>", "tag"],
          ["<[\\w:-]+", "tag", "@tag"],
          ["[^<]+", "source"]
        ],
        "comment": [
          ["[^-]+", "comment"],
          ["-->", "comment", "@pop"],
          ["-", "comment"]
        ],
        "tag": [
          ["\\s+", "white"],
          ["[\\w:-]+", "attribute.name"],
          ["=", "delimiter"],
          ["\"[^\"]*\"", "attribute.value"],
          ["'[^']*'", "attribute.value"],
          ["/?>", "tag", "@pop"]
        ]
      }
    }
    """;

    public const string CssMonarch = """
    {
      "defaultToken": "source",
      "tokenPostfix": ".css",
      "tokenizer": {
        "root": [
          ["\\/\\*", "comment", "@comment"],
          ["[.#]?[a-zA-Z_-][\\w-]*", "identifier"],
          ["@\\w+", "keyword"],
          ["\"[^\"]*\"", "string"],
          ["'[^']*'", "string"],
          ["#[0-9a-fA-F]+", "number"],
          ["\\d+(\\.\\d+)?(px|em|rem|%|vh|vw)?", "number"],
          ["[{}()\\[\\];:]", "delimiter"],
          ["\\s+", "white"]
        ],
        "comment": [
          ["[^\\*]+", "comment"],
          ["\\*\\/", "comment", "@pop"],
          ["\\*", "comment"]
        ]
      }
    }
    """;

    public const string SqlMonarch = """
    {
      "defaultToken": "source",
      "tokenPostfix": ".sql",
      "keywords": [
        "SELECT","FROM","WHERE","AND","OR","INSERT","INTO","VALUES","UPDATE","SET","DELETE","CREATE","TABLE",
        "DROP","ALTER","JOIN","LEFT","RIGHT","INNER","OUTER","ON","AS","ORDER","BY","GROUP","HAVING","LIMIT",
        "NULL","NOT","IN","IS","LIKE","BETWEEN","DISTINCT","UNION","ALL","PRIMARY","KEY","FOREIGN","REFERENCES"
      ],
      "tokenizer": {
        "root": [
          ["--.*$", "comment"],
          ["\\/\\*", "comment", "@comment"],
          ["\"[^\"]*\"", "string"],
          ["'[^']*'", "string"],
          ["[a-zA-Z_][\\w$]*", { "cases": { "@keywords": "keyword", "@default": "identifier" } }],
          ["\\d+(\\.\\d+)?", "number"],
          ["[{}()\\[\\],;.]", "delimiter"],
          ["[=<>!]+", "operator"],
          ["\\s+", "white"]
        ],
        "comment": [
          ["[^\\*]+", "comment"],
          ["\\*\\/", "comment", "@pop"],
          ["\\*", "comment"]
        ]
      }
    }
    """;

    public const string MarkdownMonarch = """
    {
      "defaultToken": "source",
      "tokenPostfix": ".md",
      "tokenizer": {
        "root": [
          ["^#{1,6}\\s.*$", "keyword"],
          ["```.*$", "string"],
          ["`[^`]+`", "string"],
          [".+", "source"]
        ]
      }
    }
    """;

    public const string ShellMonarch = """
    {
      "defaultToken": "source",
      "tokenPostfix": ".shell",
      "keywords": ["if","then","else","elif","fi","for","while","do","done","case","esac","function","in","return","exit"],
      "tokenizer": {
        "root": [
          ["#.*$", "comment"],
          ["\"", "string", "@string_double"],
          ["'", "string", "@string_single"],
          ["[a-zA-Z_]\\w*", { "cases": { "@keywords": "keyword", "@default": "identifier" } }],
          ["\\$\\w+", "variable"],
          ["\\d+", "number"],
          ["[{}()\\[\\];|&<>]", "delimiter"],
          ["\\s+", "white"]
        ],
        "string_double": [
          ["[^\\\\\"]+", "string"],
          ["\\\\.", "string.escape"],
          ["\"", "string", "@pop"]
        ],
        "string_single": [
          ["[^']+", "string"],
          ["'", "string", "@pop"]
        ]
      }
    }
    """;

    public const string YamlMonarch = """
    {
      "defaultToken": "source",
      "tokenPostfix": ".yaml",
      "tokenizer": {
        "root": [
          ["#.*$", "comment"],
          ["[\\w.-]+(?=\\s*:)", "key"],
          [":", "delimiter"],
          ["\"[^\"]*\"", "string"],
          ["'[^']*'", "string"],
          ["\\b(true|false|null|yes|no)\\b", "keyword"],
          ["-?\\d+(\\.\\d+)?", "number"],
          ["[{}\\[\\],-]", "delimiter"],
          ["\\s+", "white"],
          [".", "source"]
        ]
      }
    }
    """;
}
