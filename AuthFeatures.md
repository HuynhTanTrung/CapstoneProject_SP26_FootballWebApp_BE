
## 1. Kiến Trúc Tổng Thể

```
VNFootballLeaguesApp (Web API Layer)
├── Controllers/
│   └── AuthController.cs
├── DTOs/                        ← Request/Response models
├── Middleware/                  ← Global Exception Handler
├── Extensions/                  ← DI Registration helpers
└── Program.cs                   ← Cấu hình services

VNFootballLeagues.Services (Business Logic Layer)
├── IServices/
│   ├── IAuthService.cs
│   ├── IJwtService.cs
│   ├── IEmailService.cs
│   └── IUserService.cs
└── Services/
    ├── AuthService.cs
    ├── JwtService.cs
    ├── EmailService.cs
    └── UserService.cs

VNFootballLeagues.Repositories (Data Access Layer)
├── Models/                      ← New Auth Entities
├── Repositories/                ← IUserRepository, IRefreshTokenRepository
└── DBContext/                   ← Update DbContext
```

## 3. API Endpoints

### Base URL: `/api/auth`

| Method | Endpoint | Mô tả | Auth Required |
|--------|----------|-------|---------------|
| POST | `/api/auth/register` | Đăng ký tài khoản | ❌ |
| POST | `/api/auth/login` | Đăng nhập | ❌ |
| POST | `/api/auth/logout` | Đăng xuất (revoke refresh token) | ✅ |
| POST | `/api/auth/refresh-token` | Làm mới access token | ❌ (dùng refresh token) |
| GET | `/api/auth/verify-email` | Xác thực email qua link | ❌ |
| POST | `/api/auth/resend-verification` | Gửi lại email xác thực | ❌ |
| POST | `/api/auth/forgot-password` | Yêu cầu reset mật khẩu | ❌ |
| POST | `/api/auth/reset-password` | Đặt lại mật khẩu | ❌ |
| GET | `/api/auth/me` | Lấy thông tin user hiện tại | ✅ |

## 8. Security Implementation

### 8.1 Password Security
- BCrypt hash với cost factor 12
- Không lưu plain text
- Kiểm tra strength: min 8 ký tự, chữ hoa, chữ thường, số, ký tự đặc biệt

### 8.2 JWT Security
- Access Token: 60 phút (configurable)
- Refresh Token: 7 ngày (configurable), lưu DB để có thể revoke
- Token signing với `HS256` hoặc `RS256`
- Claims: `sub` (userId), `email`, `roles`, `jti` (unique token ID)

### 8.3 Email Verification Flow
```
Register → Gửi email xác thực → User click link → Verify token → Kích hoạt tài khoản
         ← Token hết hạn (24h) → Resend verification email
```

### 8.4 Password Reset Flow
```
Forgot Password → Gửi email reset → User click link → Validate token (1h) → Đặt mật khẩu mới → Revoke all refresh tokens
```

### 8.5 Account Lockout
- Sau 5 lần đăng nhập sai: lock tài khoản 15 phút
- Lưu `FailedLoginAttempts` và `LockoutEnd` trong User entity

### 8.6 Rate Limiting
```
/api/auth/login: 5 requests/phút/IP
/api/auth/forgot-password: 3 requests/giờ/IP
/api/auth/register: 10 requests/giờ/IP
```

### 8.7 Response Security
- Không trả về thông tin nhạy cảm trong error messages
- Message chung chung: "Email hoặc mật khẩu không đúng" (không phân biệt email/password sai)
- Luôn trả HTTP 200 cho forgot-password (tránh email enumeration)

---

## 9. Middleware & Cross-Cutting Concerns

### `GlobalExceptionHandlerMiddleware.cs`
- Bắt mọi unhandled exception
- Return `ApiResponseDto` với message thân thiện
- Log chi tiết lỗi server-side

### Authorization Policies
```csharp
// Program.cs
builder.Services.AddAuthorization(options => {
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin"));
});
```


## 13. Kiểm Tra & Xác Nhận

### Test End-to-End Flow:

**Register Flow:**
```
POST /api/auth/register → 200 (message: check email)
→ Inbox: Click link verify email
GET /api/auth/verify-email?token=xxx → 200 (account activated)
```

**Login Flow:**
```
POST /api/auth/login (chưa verify) → 400 (Email chưa xác thực)
POST /api/auth/login (đúng) → 200 (AccessToken + RefreshToken)
GET /api/auth/me (với Bearer token) → 200 (user profile)
POST /api/auth/refresh-token → 200 (new AccessToken)
POST /api/auth/logout → 200 (refresh token revoked)
```

**Forgot Password Flow:**
```
POST /api/auth/forgot-password → 200 (luôn trả 200)
→ Inbox: Click link reset password
POST /api/auth/reset-password → 200 (password changed)
POST /api/auth/login (mật khẩu mới) → 200
```

**Security Test:**
```
POST /api/auth/login (sai 5 lần) → 429 (account locked)
POST /api/auth/login (quá rate limit) → 429
GET /api/auth/me (không có token) → 401
GET /api/admin/... (role User) → 403
```


