window.fluxmqMonaco = (function () {
    const jsonataLanguageId = "jsonata";
    let configured = false;

    const jsonataFunctions = [
        "$abs", "$append", "$assert", "$average", "$base64decode", "$base64encode",
        "$boolean", "$ceil", "$contains", "$count", "$distinct", "$each", "$error",
        "$exists", "$filter", "$floor", "$formatBase", "$formatInteger", "$formatNumber",
        "$fromMillis", "$join", "$keys", "$length", "$lookup", "$lowercase", "$map",
        "$match", "$max", "$merge", "$millis", "$min", "$not", "$now", "$number",
        "$pad", "$parseInteger", "$power", "$random", "$reduce", "$replace", "$reverse",
        "$round", "$single", "$sort", "$split", "$spread", "$sqrt", "$string", "$substring",
        "$substringAfter", "$substringBefore", "$sum", "$toMillis", "$trim", "$type",
        "$uppercase", "$zip"
    ];

    function isMonacoReady() {
        return !!window.monaco && !!window.monaco.editor && !!window.monaco.languages;
    }

    function ensureConfigured(isDark, retry) {
        if (!isMonacoReady()) {
            if ((retry || 0) < 20) {
                window.setTimeout(function () { ensureConfigured(isDark, (retry || 0) + 1); }, 50);
            }
            return;
        }

        if (!configured) {
            registerJsonataLanguage();
            defineThemes();
            configured = true;
        }

        setTheme(isDark);
    }

    function registerJsonataLanguage() {
        const exists = monaco.languages.getLanguages().some(function (language) {
            return language.id === jsonataLanguageId;
        });

        if (!exists) {
            monaco.languages.register({
                id: jsonataLanguageId,
                aliases: ["JSONata", "jsonata"],
                extensions: [".jsonata"]
            });
        }

        monaco.languages.setMonarchTokensProvider(jsonataLanguageId, {
            defaultToken: "",
            tokenPostfix: ".jsonata",
            keywords: ["and", "or", "in", "not", "true", "false", "null", "function"],
            operators: [
                "~>", ":=", "..", "!=", "<=", ">=", "=", "<", ">", "&",
                "+", "-", "*", "/", "%", "?", ":", "|"
            ],
            tokenizer: {
                root: [
                    [/[{}()[\],.;]/, "delimiter"],
                    [/:=|\.\.|~>|!=|<=|>=|[=<>+*/%&?:|-]/, "operator"],
                    [/"([^"\\]|\\.)*$/, "string.invalid"],
                    [/"/, { token: "string.quote", bracket: "@open", next: "@string" }],
                    [/\/([^\\/]|\\.)+\/[im]*/, "regexp"],
                    [/-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?/, "number"],
                    [/\$[a-zA-Z_][\w-]*(?=\s*\()/, "predefined"],
                    [/\$[a-zA-Z_][\w-]*/, "variable"],
                    [/[a-zA-Z_][\w-]*/, {
                        cases: {
                            "@keywords": "keyword",
                            "@default": "identifier"
                        }
                    }]
                ],
                string: [
                    [/[^\\"]+/, "string"],
                    [/\\["\\/bfnrt]/, "string.escape"],
                    [/\\u[0-9a-fA-F]{4}/, "string.escape"],
                    [/\\./, "string.escape.invalid"],
                    [/"/, { token: "string.quote", bracket: "@close", next: "@pop" }]
                ]
            }
        });

        monaco.languages.setLanguageConfiguration(jsonataLanguageId, {
            brackets: [["{", "}"], ["[", "]"], ["(", ")"]],
            autoClosingPairs: [
                { open: "{", close: "}" },
                { open: "[", close: "]" },
                { open: "(", close: ")" },
                { open: "\"", close: "\"", notIn: ["string"] }
            ],
            surroundingPairs: [
                { open: "{", close: "}" },
                { open: "[", close: "]" },
                { open: "(", close: ")" },
                { open: "\"", close: "\"" }
            ]
        });

        monaco.languages.registerCompletionItemProvider(jsonataLanguageId, {
            triggerCharacters: ["$"],
            provideCompletionItems: function (model, position) {
                const word = model.getWordUntilPosition(position);
                const line = model.getLineContent(position.lineNumber);
                let startColumn = word.startColumn;
                if (startColumn > 1 && line.charAt(startColumn - 2) === "$") {
                    startColumn -= 1;
                }

                const range = {
                    startLineNumber: position.lineNumber,
                    endLineNumber: position.lineNumber,
                    startColumn: startColumn,
                    endColumn: word.endColumn
                };

                return {
                    suggestions: jsonataFunctions.map(function (name) {
                        return {
                            label: name,
                            kind: monaco.languages.CompletionItemKind.Function,
                            insertText: name + "()",
                            range: range
                        };
                    })
                };
            }
        });
    }

    function defineThemes() {
        monaco.editor.defineTheme("fluxmq-dark", {
            base: "vs-dark",
            inherit: true,
            rules: [
                { token: "keyword.jsonata", foreground: "60A5FA" },
                { token: "predefined.jsonata", foreground: "2DD4BF" },
                { token: "variable.jsonata", foreground: "34D399" },
                { token: "string.jsonata", foreground: "FBBF24" },
                { token: "number.jsonata", foreground: "F87171" },
                { token: "regexp.jsonata", foreground: "C084FC" },
                { token: "operator.jsonata", foreground: "A0A8B4" },
                { token: "delimiter.jsonata", foreground: "E8ECF2" }
            ],
            colors: {
                "editor.background": "#11151C",
                "editor.foreground": "#E8ECF2",
                "editorLineNumber.foreground": "#6B7280",
                "editorLineNumber.activeForeground": "#A0A8B4",
                "editorCursor.foreground": "#2DD4BF",
                "editor.selectionBackground": "#2563EB55",
                "editor.lineHighlightBackground": "#161B24",
                "editorGutter.background": "#11151C",
                "editorIndentGuide.background1": "#1D232E",
                "editorIndentGuide.activeBackground1": "#2A313D",
                "editorWidget.background": "#161B24",
                "editorWidget.border": "#2A313D"
            }
        });

        monaco.editor.defineTheme("fluxmq-light", {
            base: "vs",
            inherit: true,
            rules: [
                { token: "keyword.jsonata", foreground: "2563EB" },
                { token: "predefined.jsonata", foreground: "0F766E" },
                { token: "variable.jsonata", foreground: "059669" },
                { token: "string.jsonata", foreground: "B45309" },
                { token: "number.jsonata", foreground: "DC2626" },
                { token: "regexp.jsonata", foreground: "7C3AED" },
                { token: "operator.jsonata", foreground: "526174" },
                { token: "delimiter.jsonata", foreground: "111827" }
            ],
            colors: {
                "editor.background": "#FFFFFF",
                "editor.foreground": "#111827",
                "editorLineNumber.foreground": "#94A3B8",
                "editorLineNumber.activeForeground": "#526174",
                "editorCursor.foreground": "#0F766E",
                "editor.selectionBackground": "#2563EB33",
                "editor.lineHighlightBackground": "#F7F9FB",
                "editorGutter.background": "#FFFFFF",
                "editorIndentGuide.background1": "#DDE3EA",
                "editorIndentGuide.activeBackground1": "#C3CCD8",
                "editorWidget.background": "#FFFFFF",
                "editorWidget.border": "#C3CCD8"
            }
        });
    }

    function setTheme(isDark) {
        if (!isMonacoReady()) {
            return;
        }

        monaco.editor.setTheme(isDark ? "fluxmq-dark" : "fluxmq-light");
    }

    return {
        ensureConfigured: ensureConfigured,
        setTheme: setTheme
    };
})();

window.fluxmqMonaco.ensureConfigured(
    !!window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches
);
