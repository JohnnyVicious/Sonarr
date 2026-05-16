# API integration test helpers

Use `ApiV3` and `ApiV5` from `IntegrationTestBase` for new endpoint coverage. Each client has its own versioned API root, so a fixture can call both API surfaces without hardcoding `/api/v3` as the global base path.

Prefer the shared helpers for new tests:

- `ApiV3.Get("system/status")` or `ApiV5.Get("system/status")` for authenticated requests.
- `ApiV3.Get("system/status", authenticated: false)` when the request should intentionally omit API-key headers.
- `response.ShouldHaveJsonObjectContent()`, `response.ShouldHaveJsonArrayContent()`, and `response.ShouldHaveValidationErrors()` for common response assertions.
- `ApiV3.OpenApi.ShouldDeclareResponse(...)` and `ApiV5.OpenApi.ShouldDeclareResponse(...)` when a test should assert that the checked-in OpenAPI contract declares the operation/status it exercises.
