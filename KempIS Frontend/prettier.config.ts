import type { Config } from "prettier";

const config: Config = {
  arrowParens: "avoid",
  bracketSpacing: true,
  bracketSameLine: false,
  endOfLine: "lf",
  htmlWhitespaceSensitivity: "css",
  objectWrap: "preserve",
  quoteProps: "consistent",
  semi: true,
  singleAttributePerLine: true,
  singleQuote: false,
  tabWidth: 2,
  trailingComma: "es5",
  useTabs: false,
  overrides: [
    {
      files: "*.html",
      options: {
        parser: "angular",
      },
    },
  ],
};

export default config;
