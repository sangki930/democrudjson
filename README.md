# PostgreSQL CRUD 테스트 API

C# / ASP.NET Core MVC (.NET 10) / Entity Framework Core 10 / Npgsql 기반 테스트 백엔드입니다.

`LAYER` 테이블의 JSONB CRUD API(`/api/layers`)와 스키마는 [README_TABLE.md](README_TABLE.md)를 참고하세요.

## 구조와 엔티티 매핑

`HTTP 요청 → Controller → Service → AppDbContext → PostgreSQL` 구조입니다.

- `Controllers/ItemsController.cs`: 라우팅, HTTP 상태 코드와 JSON 응답
- `Controllers/SystemController.cs`: API 안내 및 DB 상태 확인
- `Models/Entities/Item.cs`: DB 테이블에 매핑되는 엔티티
- `Models/Requests/ItemInput.cs`: 생성·수정 입력 및 MVC 자동 검증
- `Models/Responses/ItemResponse.cs`: 엔티티와 분리된 응답 DTO
- `Services/ItemService.cs`: CRUD 처리, LINQ 조회와 저장
- `Data/AppDbContext.cs`: 요청 범위로 주입하는 EF Core DbContext와 `DbSet<Item>`
- `Data/Configurations/ItemConfiguration.cs`: Fluent API를 통한 테이블·컬럼·제약조건 매핑
- `Data/Migrations/`: 테이블 변경 이력과 모델 스냅샷
- `Data/AppDbContextFactory.cs`: EF CLI용 컨텍스트 생성

ASP.NET Core MVC의 `ControllerBase`와 `[ApiController]`를 사용하는 Web API입니다. 현재 프런트엔드 화면은 없으므로 Razor View 대신 JSON 응답을 제공합니다.

| Item 속성 | PostgreSQL 컬럼 | DB 타입 |
|---|---|---|
| Id | id | uuid (PK) |
| Name | name | varchar(200), NOT NULL |
| Description | description | varchar(2000), NULL 허용 |
| IsCompleted | is_completed | boolean |
| CreatedAt | created_at | timestamptz |
| UpdatedAt | updated_at | timestamptz |

테이블은 `democrud_demo.items`입니다. 생성 시각은 DB의 `now()` 기본값을 사용하고 수정 시각은 서비스에서 UTC로 갱신합니다. 조회는 `AsNoTracking`, 저장은 `SaveChangesAsync`, 삭제는 `ExecuteDeleteAsync`로 처리합니다. 클라이언트가 ID나 생성 시각을 덮어쓰지 못하도록 요청 DTO에는 해당 속성을 포함하지 않습니다.

엔티티·매핑 수정 후 `dotnet ef migrations add 변경이름 --output-dir Data/Migrations`로 변경 파일을 생성하세요. 개발 환경에서는 다음 앱 시작 시 자동 적용됩니다. 자세한 절차는 [README_MIGRATIONS.md](README_MIGRATIONS.md)를 참고하세요.

## 연결 대상

- Docker 컨테이너: `postgres-dev` (`postgres:17-alpine`)
- 호스트 실행 기준: `127.0.0.1:5432`
- 데이터베이스: `app_dev`, API 전용 사용자: `democrud_demo_user`
- 전용 테이블: `democrud_demo.items`

현재 DB에는 API 전용 계정과 소유 스키마를 생성해 두었습니다. Development 환경에서 시작하면 미적용 EF Core 마이그레이션을 실행합니다. Production에서는 자동 적용하지 않습니다. DB가 실행 중이어야 하며 마이그레이션 적용 계정에 스키마 내 DDL 권한이 필요합니다.

다른 DB에 설치할 때는 관리자가 API 계정을 준비하고 `CREATE SCHEMA democrud_demo AUTHORIZATION democrud_demo_user;`를 먼저 실행하세요. 기존 개발 계정의 비밀번호는 변경하지 않았습니다.

## 실행

현재 환경의 연결 문자열은 Git 제외 파일 `appsettings.Local.json`에 저장되어 있습니다.

```powershell
dotnet run
```

다른 환경에서는 `ConnectionStrings__Postgres` 환경변수 또는 다음 형식의 `appsettings.Local.json`을 설정하세요. 환경변수가 로컬 파일보다 우선합니다. 비밀번호는 저장소에 커밋하지 마세요.

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=127.0.0.1;Port=5432;Database=app_dev;Username=democrud_demo_user;Password=YOUR_PASSWORD;Timeout=5;Command Timeout=15"
  }
}
```

API 주소: `http://localhost:5080`. API 자체를 컨테이너로 실행한다면 DB와 동일한 Docker 네트워크에서 `Host=postgres-dev;Port=5432`를 사용해야 합니다.

## API

| 메서드 | 경로 | 응답 |
|---|---|---|
| GET | `/health` | DB 정상 200 / 연결 실패 503 |
| POST | `/api/items` | 생성 201 및 Location 헤더 |
| GET | `/api/items` | 최신 100개 목록 200 |
| GET | `/api/items/{id}` | 단건 200 / 없음 404 |
| PUT | `/api/items/{id}` | 전체 수정 200 / 없음 404 |
| DELETE | `/api/items/{id}` | 삭제 204 / 없음 404 |

생성·수정 요청:

```json
{"name":"테스트 항목","description":"설명","isCompleted":false}
```

`name`은 공백 제거 후 1~200자, `description`은 생략 또는 null 가능하며 최대 2,000자입니다. 검증 실패는 400입니다. `PUT`에서 생략한 설명/완료 여부는 null/false로 교체됩니다. 응답에는 UUID `id`와 UTC `createdAt`, `updatedAt`이 포함됩니다. SQL에는 매개변수를 사용합니다.

수동 요청 예시는 `DemoCrud.Api.http`를 사용하세요. 인증 없는 로컬 개발용 API입니다.

## 실제 DB 통합 검증

API를 실행한 상태에서 별도 PowerShell 7 터미널에서:

```powershell
pwsh -File scripts/Test-Crud.ps1
```

DB 연결, 생성→조회→수정→삭제, 한글/따옴표 저장, 입력 검증, 없는 ID의 404 응답을 검사합니다. 테스트가 생성한 항목만 삭제합니다.

DB 저장 상태는 다음 명령으로 확인할 수 있습니다.

```powershell
docker exec postgres-dev psql -U app_dev_user -d app_dev -c "SELECT * FROM democrud_demo.items;"
```

구현 참고: [Npgsql EF Core](https://www.npgsql.org/efcore/), [ASP.NET Core 컨트롤러 Web API](https://learn.microsoft.com/aspnet/core/web-api/?view=aspnetcore-10.0).
