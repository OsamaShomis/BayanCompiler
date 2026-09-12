"""فحص مستقل لبنية جدول LL(1) الموثقة لمترجم بيان.

هذا الملف لا يستبدل اختبارات C#؛ بل يتحقق من أن خريطة القواعد نفسها
تقبل سلاسل Tokens النموذجية وترفض البنية الناقصة قبل فتح المشروع في Visual Studio.
"""

from __future__ import annotations


STATEMENT_STARTS = {"Let", "Identifier", "Print", "If", "While", "LeftBrace"}
STATEMENT_FOLLOW = STATEMENT_STARTS | {"End", "RightBrace"}
EXPR_STARTS = {"Bang", "Minus", "Integer", "Real", "String", "True", "False", "Identifier", "LeftParen"}


def add(table: dict[tuple[str, str], tuple[str, ...]], nonterminal: str, lookaheads: set[str], *production: str) -> None:
    """يضيف إنتاجاً واحداً إلى كل خلية LL(1) مع كشف التعارضات."""
    for lookahead in lookaheads:
        key = (nonterminal, lookahead)
        if key in table:
            raise AssertionError(f"LL(1) conflict at {key}: {table[key]} versus {production}")
        table[key] = production


def build_table() -> dict[tuple[str, str], tuple[str, ...]]:
    """ينشئ جدولاً مطابقاً للوثيقة وLl1Parser.cs."""
    table: dict[tuple[str, str], tuple[str, ...]] = {}
    add(table, "Start", {"Program"}, "ProgramNode", "Eof")
    add(table, "ProgramNode", {"Program"}, "Program", "Identifier", "StatementList", "End")
    add(table, "StatementList", STATEMENT_STARTS, "Statement", "StatementList")
    add(table, "StatementList", {"End", "RightBrace"})
    add(table, "Statement", {"Let"}, "Declaration")
    add(table, "Statement", {"Identifier"}, "Assignment")
    add(table, "Statement", {"Print"}, "PrintStatement")
    add(table, "Statement", {"If"}, "IfStatement")
    add(table, "Statement", {"While"}, "WhileStatement")
    add(table, "Statement", {"LeftBrace"}, "Block")
    add(table, "Declaration", {"Let"}, "Let", "Identifier", "Colon", "Type", "InitializerOpt", "Semicolon")
    add(table, "InitializerOpt", {"Assign"}, "Assign", "Expression")
    add(table, "InitializerOpt", {"Semicolon"})
    for kind in ("TypeInt", "TypeReal", "TypeBool", "TypeText"):
        add(table, "Type", {kind}, kind)
    add(table, "Assignment", {"Identifier"}, "Identifier", "Assign", "Expression", "Semicolon")
    add(table, "PrintStatement", {"Print"}, "Print", "LeftParen", "Expression", "RightParen", "Semicolon")
    add(table, "IfStatement", {"If"}, "If", "LeftParen", "Expression", "RightParen", "Block", "ElsePart")
    add(table, "ElsePart", {"Else"}, "Else", "Block")
    add(table, "ElsePart", STATEMENT_FOLLOW)
    add(table, "WhileStatement", {"While"}, "While", "LeftParen", "Expression", "RightParen", "Block")
    add(table, "Block", {"LeftBrace"}, "LeftBrace", "StatementList", "RightBrace")

    add(table, "Expression", EXPR_STARTS, "Or")
    add(table, "Or", EXPR_STARTS, "And", "OrTail")
    add(table, "OrTail", {"OrOr"}, "OrOr", "And", "OrTail")
    add(table, "OrTail", {"Semicolon", "RightParen"})
    add(table, "And", EXPR_STARTS, "Equality", "AndTail")
    add(table, "AndTail", {"AndAnd"}, "AndAnd", "Equality", "AndTail")
    add(table, "AndTail", {"OrOr", "Semicolon", "RightParen"})
    add(table, "Equality", EXPR_STARTS, "Comparison", "EqualityTail")
    for kind in ("EqualEqual", "BangEqual"):
        add(table, "EqualityTail", {kind}, kind, "Comparison", "EqualityTail")
    add(table, "EqualityTail", {"AndAnd", "OrOr", "Semicolon", "RightParen"})
    add(table, "Comparison", EXPR_STARTS, "Addition", "ComparisonTail")
    for kind in ("Less", "LessEqual", "Greater", "GreaterEqual"):
        add(table, "ComparisonTail", {kind}, kind, "Addition", "ComparisonTail")
    add(table, "ComparisonTail", {"EqualEqual", "BangEqual", "AndAnd", "OrOr", "Semicolon", "RightParen"})
    add(table, "Addition", EXPR_STARTS, "Multiplication", "AdditionTail")
    for kind in ("Plus", "Minus"):
        add(table, "AdditionTail", {kind}, kind, "Multiplication", "AdditionTail")
    add(table, "AdditionTail", {"Less", "LessEqual", "Greater", "GreaterEqual", "EqualEqual", "BangEqual", "AndAnd", "OrOr", "Semicolon", "RightParen"})
    add(table, "Multiplication", EXPR_STARTS, "Unary", "MultiplicationTail")
    for kind in ("Star", "Slash", "Percent"):
        add(table, "MultiplicationTail", {kind}, kind, "Unary", "MultiplicationTail")
    add(table, "MultiplicationTail", {"Plus", "Minus", "Less", "LessEqual", "Greater", "GreaterEqual", "EqualEqual", "BangEqual", "AndAnd", "OrOr", "Semicolon", "RightParen"})
    add(table, "Unary", {"Bang"}, "Bang", "Unary")
    add(table, "Unary", {"Minus"}, "Minus", "Unary")
    add(table, "Unary", {"Integer", "Real", "String", "True", "False", "Identifier", "LeftParen"}, "Primary")
    for kind in ("Integer", "Real", "String", "True", "False", "Identifier"):
        add(table, "Primary", {kind}, kind)
    add(table, "Primary", {"LeftParen"}, "LeftParen", "Expression", "RightParen")
    return table


def parse(table: dict[tuple[str, str], tuple[str, ...]], tokens: list[str]) -> bool:
    """ينفذ خوارزمية Stack الجدولية ويعيد النجاح أو الفشل."""
    terminals = {
        "Program", "Identifier", "End", "Let", "If", "Else", "While", "Print", "TypeInt", "TypeReal", "TypeBool", "TypeText",
        "True", "False", "Plus", "Minus", "Star", "Slash", "Percent", "Assign", "EqualEqual", "Bang", "BangEqual", "Less",
        "LessEqual", "Greater", "GreaterEqual", "AndAnd", "OrOr", "LeftParen", "RightParen", "LeftBrace", "RightBrace", "Colon", "Semicolon", "Integer", "Real", "String", "Eof",
    }
    stack = ["Start"]
    position = 0
    while stack:
        symbol = stack.pop()
        lookahead = tokens[position]
        if symbol in terminals:
            if symbol != lookahead:
                return False
            position += 1
            continue
        production = table.get((symbol, lookahead))
        if production is None:
            return False
        stack.extend(reversed(production))
    return position == len(tokens)


def main() -> None:
    """ينفذ حالات تمثل التعريف والشرط والحلقة والأسبقية والخطأ النحوي."""
    table = build_table()
    cases = {
        "تعريف وطباعة": ["Program", "Identifier", "Let", "Identifier", "Colon", "TypeInt", "Assign", "Integer", "Semicolon", "Print", "LeftParen", "Identifier", "RightParen", "Semicolon", "End", "Eof"],
        "حلقة وإسناد": ["Program", "Identifier", "Let", "Identifier", "Colon", "TypeInt", "Assign", "Integer", "Semicolon", "While", "LeftParen", "Identifier", "LessEqual", "Integer", "RightParen", "LeftBrace", "Identifier", "Assign", "Identifier", "Plus", "Integer", "Semicolon", "RightBrace", "End", "Eof"],
        "شرط وإلا": ["Program", "Identifier", "If", "LeftParen", "True", "RightParen", "LeftBrace", "Print", "LeftParen", "String", "RightParen", "Semicolon", "RightBrace", "Else", "LeftBrace", "Print", "LeftParen", "String", "RightParen", "Semicolon", "RightBrace", "End", "Eof"],
        "أولوية العمليات": ["Program", "Identifier", "Let", "Identifier", "Colon", "TypeBool", "Assign", "True", "OrOr", "False", "AndAnd", "Bang", "False", "Semicolon", "End", "Eof"],
    }
    for name, tokens in cases.items():
        if not parse(table, tokens):
            raise AssertionError(f"LL(1) rejected valid case: {name}")
    invalid = ["Program", "Identifier", "Let", "Identifier", "Colon", "TypeInt", "Assign", "Integer", "End", "Eof"]
    if parse(table, invalid):
        raise AssertionError("LL(1) accepted a declaration without Semicolon")
    print(f"LL(1) table validation passed: {len(table)} populated cells, {len(cases)} valid cases, 1 invalid case.")


if __name__ == "__main__":
    main()
