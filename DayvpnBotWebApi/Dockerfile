# مرحله 1: بیس (Runtime فقط برای اجرای اپلیکیشن)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# مرحله 2: بیلد (شامل SDK برای build, restore و publish)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# کپی سورس‌کد به مرحله build
COPY . .

# بازیابی پکیج‌ها
RUN dotnet restore "DayvpnBotWebApi.csproj"

# پابلیش اپلیکیشن
RUN dotnet publish "DayvpnBotWebApi.csproj" -c Release -o /app/publish

# مرحله 3: انتقال خروجی پابلیش به مرحله نهایی
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "DayvpnBotWebApi.dll"]
