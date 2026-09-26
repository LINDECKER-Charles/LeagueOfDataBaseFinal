// Files imported with `with { loader: 'text' }`: the application builder inlines them as a
// string. Only specs use it, to test markup that cannot be imported as code.
declare module '*.html' {
  const text: string;
  export default text;
}
