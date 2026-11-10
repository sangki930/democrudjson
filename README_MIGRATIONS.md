# 엔티티 변경과 DB 마이그레이션 가이드

## 현재 동작

EF Core 10 Migration을 사용합니다. `Development` 환경에서 API가 시작되면 `Database.MigrateAsync()`가 아직 적용하지 않은 마이그레이션을 순서대로 실행합니다. Production 등 다른 환경에서는 시작 시 DB 구조를 변경하지 않습니다.

**엔티티만 수정해도 DB가 자동 변경되는 것은 아닙니다. 마이그레이션 파일을 생성해야 합니다.** EF는 현재 모델과 `AppDbContextModelSnapshot`을 비교하여 변경 코드를 생성합니다. 모델만 수정하고 마이그레이션을 빠뜨리면 개발 서버 시작 시 오류로 알려줍니다.

- `Data/Migrations/`: 변경 코드, Designer 메타데이터, 모델 스냅샷
- `democrud_demo."__EFMigrationsHistory"`: DB에 적용한 이력
- `Data/AppDbContextFactory.cs`: API를 실행하지 않고 EF CLI에 DB 설정 제공
- `dotnet-tools.json`: 프로젝트용 EF CLI 버전 고정
- `Properties/launchSettings.json`: `dotnet run`의 Development 프로필

## 최초 준비

프로젝트 루트에서 실행합니다.

```powershell
dotnet tool restore
dotnet restore
dotnet run
```

연결 문자열은 기존 `appsettings.Local.json` 또는 `ConnectionStrings__Postgres` 환경변수를 사용합니다. DB와 `democrud_demo` 스키마는 사전에 준비되어 있어야 하며 연결 계정은 해당 스키마 내 DDL 권한이 필요합니다. 자동으로 DB나 계정을 만들지는 않습니다.

기존 `app_dev`의 items/LAYER 테이블은 초기 `InitialSchema`에서 데이터를 유지하면서 편입합니다. 이 최초 마이그레이션만 `CREATE TABLE IF NOT EXISTS`를 사용합니다. 다른 기존 DB에 적용하기 전에는 테이블 구조가 README_TABLE.md 및 ItemConfiguration과 일치하는지 확인하세요. 이 초기 전환은 임의의 스키마 차이를 자동 보정하지 않습니다. 예전 varchar(16) DB라면 `Data/Scripts/002_LayerObjectId24.sql`로 먼저 확장하세요.

## 엔티티를 변경할 때

1. 실행 중인 API를 Ctrl+C로 중지합니다. Windows에서는 실행 중인 DLL 때문에 빌드가 잠길 수 있습니다.
2. `Models/Entities`의 엔티티와 `Data/Configurations`의 매핑을 함께 수정합니다. API에 노출할 필드라면 Request/Response와 서비스도 수정합니다.
3. 의미 있는 이름으로 마이그레이션을 생성합니다.

```powershell
# 예: 실제로 새 속성과 매핑을 추가한 뒤 실행
dotnet ef migrations add AddLayerTitle --output-dir Data/Migrations
```

4. 생성된 `Up`과 `Down`을 확인합니다. 이름 변경은 DropColumn/AddColumn 대신 RenameColumn인지 확인하고, 기존 행이 있는 테이블에 NOT NULL 컬럼을 추가할 때는 기본값이나 데이터 보완을 고려하세요.
5. 개발 서버를 시작하면 자동 적용됩니다.

```powershell
dotnet run
```

서버 시작 없이 수동 적용하려면:

```powershell
dotnet ef database update
```

6. 별도 터미널에서 API를 검증합니다.

```powershell
pwsh -File scripts/Test-LayerCrud.ps1
pwsh -File scripts/Test-Crud.ps1
```

엔티티, 매핑, 마이그레이션, Designer, 스냅샷을 함께 커밋합니다. 비밀번호가 있는 로컬 설정은 커밋하지 않습니다.

`Layer.Data` 내부에 JSON 키를 추가하는 것은 jsonb 내부 데이터 변경이므로 컬럼 마이그레이션이 필요하지 않습니다. 엔티티에 별도 DB 컬럼을 추가하거나 컬럼 타입·제약을 바꿀 때 필요합니다.

## 상태 확인

```powershell
dotnet ef migrations list
dotnet ef migrations has-pending-model-changes
```

두 번째 명령은 모델과 스냅샷의 차이를 확인합니다. DB 적용 여부는 첫 번째 명령과 아래 이력 조회로 확인합니다.

```sql
SELECT * FROM democrud_demo."__EFMigrationsHistory" ORDER BY "MigrationId";
```

## 운영 환경

Production에서는 시작 시 자동 적용하지 않습니다. 배포 전에 SQL을 생성·검토하여 적용하거나 배포 단계에서 `dotnet ef database update`를 실행합니다.

```powershell
dotnet ef migrations script --idempotent --output migration.sql
$env:ASPNETCORE_ENVIRONMENT = 'Production'
dotnet run --no-launch-profile
```

접속 대상은 로컬 설정 또는 환경변수가 결정합니다. 운영 대상 연결 문자열을 확인하고 적용하세요. `dotnet run`은 기본 개발 프로필을 사용하므로 Production 테스트에는 `--no-launch-profile`이 필요합니다.

## 수정·되돌리기

아직 DB에 적용하지 않은 마지막 마이그레이션은 `dotnet ef migrations remove`로 제거하고 다시 생성할 수 있습니다. 이미 적용한 마이그레이션은 지우거나 고치지 말고 후속 마이그레이션을 추가하는 방식을 권장합니다.

초기 `InitialSchema`는 기존 데이터를 편입하므로 `Down`을 차단했습니다. `database update 0`으로 기존 테이블을 삭제하지 않습니다. 이후 마이그레이션 롤백은 해당 Down의 데이터 손실 가능성을 검토해야 합니다.

참고: [EF Core 마이그레이션 적용](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).
