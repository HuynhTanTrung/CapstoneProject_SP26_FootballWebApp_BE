


---

## Function 6: CreatePost (Forum)

**Function Code:** FO_FUNC_01 | **Class:** ForumService | **Lines of Code:** 45
**Precondition:** Connected to server
**Test Requirement:** Create a forum post. Requires active subscription with ForumPostCreditsRemaining > 0. Post is saved with status "pending" and AI moderation runs async to approve/reject based on football relevance.

|  | UTCID01 | UTCID02 | UTCID03 | UTCID04 | UTCID05 | UTCID06 | UTCID07 | UTCID08 | UTCID09 |
|---|---|---|---|---|---|---|---|---|---|
| **Condition** | | | | | | | | | |
| **Authentication** | | | | | | | | | |
| Authenticated | O | O | O | O | O | O | O | | O |
| Not authenticated | | | | | | | | O | |
| **Title** | | | | | | | | | |
| = valid string | O | O | O | O | O | | O | | O |
| = "" (empty) | | | | | | O | | | |
| **Content** | | | | | | | | | |
| = valid football-related content | O | O | O | | | | | | O |
| = invalid (non-football content) | | | | O | | | | | |
| = "" (empty) | | | | | | O | | | |
| = toxic/offensive content | | | | | | | O | | |
| **leagueTag** | | | | | | | | | |
| = valid | O | O | O | | | | | | O |
| = invalid | | | | | O | | | | |
| **Subscription** | | | | | | | | | |
| Active | O | O | O | O | | O | O | | O |
| Expired | | | | | O | | | | |
| **ForumPostCreditsRemaining** | | | | | | | | | |
| > 0 | O | O | O | O | | O | O | | |
| = 0 | | | | | | | | | O |
| **Media** | | | | | | | | | |
| With media (image URL) | | O | | | | | | | |
| No media | O | | O | O | O | O | O | | O |
| **Confirm** | | | | | | | | | |
| Return Success = true | O | O | O | | | | | | |
| Return Success = false | | | | O | O | O | O | | O |
| Return 401 Unauthorized | | | | | | | | O | |
| Post saved to DB with Status = "pending" | O | O | O | | | | | | |
| ForumPostCreditsRemaining decremented by 1 | O | O | O | | | | | | |
| AI moderation runs async | O | O | O | O | | | | | |
| Post Status updated to "approved" after AI check | O | O | O | | | | | | |
| Post Status updated to "rejected" after AI check | | | | O | | | O | | |
| Message: "Bài đăng đang chờ kiểm duyệt." | O | O | O | | | | | | |
| Message: "Bạn cần đăng ký gói để đăng bài." | | | | | O | | | | |
| Message: "Bạn đã hết lượt đăng bài." | | | | | | | | | O |
| **Type (N/A/B)** | N | N | N | A | A | A | A | A | B |
| **Passed/Failed** | P | P | P | P | P | P | P | P | P |
| **Executed Date** | 16/04 | 16/04 | 16/04 | 16/04 | 16/04 | 16/04 | 16/04 | 16/04 | 16/04 |
| **Defect ID** | | | | | | | | | |
