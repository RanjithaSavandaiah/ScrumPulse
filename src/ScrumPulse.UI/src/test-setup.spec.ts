// Global test sentinel: intercepts any console.warn or console.error during Angular unit tests
// and fails the test if any unhandled error or warning is introduced.

let capturedConsoleErrors: string[] = [];
let capturedConsoleWarnings: string[] = [];

const originalConsoleError = console.error;
const originalConsoleWarn = console.warn;

beforeAll(() => {
  console.error = (...args: any[]) => {
    capturedConsoleErrors.push(args.map(a => typeof a === 'object' ? JSON.stringify(a) : String(a)).join(' '));
    originalConsoleError.apply(console, args);
  };

  console.warn = (...args: any[]) => {
    capturedConsoleWarnings.push(args.map(a => typeof a === 'object' ? JSON.stringify(a) : String(a)).join(' '));
    originalConsoleWarn.apply(console, args);
  };
});

afterEach(() => {
  if (capturedConsoleErrors.length > 0) {
    const errs = [...capturedConsoleErrors];
    capturedConsoleErrors = [];
    fail(`[CI/CD Sentinel] Unhandled console.error was introduced in unit test:\n${errs.join('\n')}`);
  }

  if (capturedConsoleWarnings.length > 0) {
    const warns = [...capturedConsoleWarnings];
    capturedConsoleWarnings = [];
    fail(`[CI/CD Sentinel] Unhandled console.warn was introduced in unit test:\n${warns.join('\n')}`);
  }
});

afterAll(() => {
  console.error = originalConsoleError;
  console.warn = originalConsoleWarn;
});
