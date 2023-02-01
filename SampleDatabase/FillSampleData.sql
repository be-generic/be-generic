INSERT [dbo].[Roles] ([Id], [RoleName], [RoleDescription]) VALUES (N'e66693a9-f822-4aa9-b061-d7c5656b8142', N'admin', N'Main administrator role.')
GO
INSERT [dbo].[Accounts] ([Username], [EmailAddress], [PasswordHash], [RoleId]) VALUES (N'test', N'test@test.test', 0x9D7624149040E53C867B720D23589AE2F3BDF7E19DABB23206158FBB293F2C491A543EB4, N'e66693a9-f822-4aa9-b061-d7c5656b8142')
GO