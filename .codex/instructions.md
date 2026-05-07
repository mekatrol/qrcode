# Coding Style

- Do not introduce magic strings or magic numbers.
- Define literal strings and numbers as named constants when they carry domain meaning, protocol meaning, configuration meaning, validation meaning, UI/output meaning, or test-fixture meaning.
- Add a concise comment for each constant explaining what it is used for and why that value is required.
- Inline literals are acceptable only when they are structurally obvious and conventional, such as loop indexes, array indexes, boolean values, `null`, empty arrays, or arithmetic identities that do not carry independent domain meaning.
- Large specification lookup tables may keep their literal table entries inline, but the table constant or field must be commented with the source meaning and why the table values stay grouped.
