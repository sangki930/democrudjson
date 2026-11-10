# LAYER 테이블과 CRUD API

## 스키마

- DB: `app_dev` (Docker `postgres-dev`, `127.0.0.1:5432`)
- 테이블: `democrud_demo."LAYER"`
- 엔티티: `Models/Entities/Layer.cs`
- EF Core 매핑: `Data/Configurations/LayerConfiguration.cs`

| 컬럼 | PostgreSQL 타입 | 제약 | 설명 |
|---|---|---|---|
| id | varchar(24) | PRIMARY KEY, NOT NULL | 외부에서 전달하는 24자리 ObjectId |
| data | jsonb | NOT NULL, JSON 객체 | 레이어 JSON 전체 |

```sql
CREATE TABLE IF NOT EXISTS democrud_demo."LAYER" (
    id varchar(24) PRIMARY KEY CHECK (length(trim(id)) > 0),
    data jsonb NOT NULL CHECK (jsonb_typeof(data) = 'object')
);
```

PostgreSQL에서 대문자 테이블명을 유지하기 위해 SQL에는 `"LAYER"`처럼 큰따옴표가 필요합니다. Development 환경에서는 API 시작 시 생성된 EF Core 마이그레이션을 자동 적용합니다. 엔티티 변경 후의 파일 생성·적용 절차는 [README_MIGRATIONS.md](README_MIGRATIONS.md)를 참고하세요.

## ID 규칙

ID는 외부 MongoDB ObjectId의 **24자리 16진수 문자열 전체**를 전달합니다. 서버에서 생성하거나 자르지 않고 요청 값을 그대로 저장합니다. 정확히 24자이며 `0-9`, `a-f`, `A-F`만 허용하고, 생성 시 그 외 값은 400을 반환합니다. ID 문자열은 대소문자를 구분하므로 조회·수정·삭제에도 저장한 표기를 사용하세요.

기존 DB 컬럼 확장 스크립트는 `Data/Scripts/002_LayerObjectId24.sql`입니다. 기존 환경에는 아래 변경을 적용했으며, 다른 기존 DB에는 한 번 실행해야 합니다. 신규 DB는 시작 시 `varchar(24)`로 생성합니다. 기존 ID와 JSON 값은 변환하지 않습니다.

```sql
ALTER TABLE democrud_demo."LAYER" ALTER COLUMN id TYPE varchar(24);
```

행의 `id`와 JSON 내부의 `layer_id`는 별개 값이며 자동 동기화하지 않습니다.

## JSON 저장 방식

`data`는 문자열로 이중 인코딩하지 않은 JSON 객체로 전달합니다. `JsonElement`를 EF Core의 `jsonb` 컬럼에 매핑하여 객체 전체를 저장합니다. `mark_ups`와 `tag`는 별도 테이블이나 컬럼으로 분리하지 않습니다.

필드명(`layer_id`, `mark_ups`, `tag_idx` 등), 중첩 구조, 배열 순서, 값의 타입과 추가 필드를 유지합니다. 내부 필드를 고정 DTO로 제한하지 않으며, `data`가 객체인지 검증합니다. 누락, null, 배열 또는 문자열 형태의 `data`는 400입니다. jsonb 특성상 공백·들여쓰기·객체 키 순서는 보존하지 않고 중복 키는 정규화됩니다.

## CRUD

기본 주소: `http://localhost:5080`

| 메서드 | 경로 | 동작 / 상태 |
|---|---|---|
| POST | `/api/layers` | 생성 201, 중복 ID 409, 입력 오류 400 |
| GET | `/api/layers` | ID 오름차순 최대 100개, 200 |
| GET | `/api/layers/{id}` | 조회 200, 없음 404 |
| PUT | `/api/layers/{id}` | data 전체 교체 200, 없음 404, 입력 오류 400 |
| PATCH | `/api/layers/{id}` | data 부분 수정 200, 없음 404, 오류 400, test 실패 409 |
| DELETE | `/api/layers/{id}` | 삭제 204, 없음 404 |

### 생성

`POST /api/layers`

```json
{
  "id": "507f1f77bcf86cd799439011",
  "data": {
    "layer_id": "sadasdnbvns",
    "layer_desc": "",
    "mark_ups": [
      { "mark_up_id": 1, "mark_up_name": "abcd" }
    ],
    "tag": {
      "tag_idx": "dasdfasdaa",
      "tag_desc": "레이어내의 부품"
    }
  }
}
```

생성·단건 조회·수정 응답은 위와 같은 `{ "id": "...", "data": { ... } }` 구조입니다. 생성 응답의 `Location`에는 단건 조회 주소가 들어갑니다.

### 수정

`PUT /api/layers/507f1f77bcf86cd799439011`

```json
{
  "data": {
    "layer_id": "sadasdnbvns",
    "layer_desc": "수정된 레이어",
    "mark_ups": [
      { "mark_up_id": 1, "mark_up_name": "수정된 마크업" }
    ],
    "tag": {
      "tag_idx": "dasdfasdaa",
      "tag_desc": "레이어내의 부품"
    }
  }
}
```

PUT은 일부 필드를 병합하지 않고 `data` 전체를 교체합니다. 따라서 유지할 필드도 함께 보내야 합니다. PK는 경로의 ID로 고정됩니다.

## 실행·검증

### JSON Patch: 중첩 객체와 배열의 부분 수정

`PATCH /api/layers/{id}`, `Content-Type: application/json-patch+json`으로 작업 배열을 전달합니다. 실행 예제는 [Layer.Patch.http](Layer.Patch.http), 자동 테스트는 `pwsh -File scripts/Test-LayerPatch.ps1`입니다.

```json
[
  { "op": "replace", "path": "/data/tag/tag_desc", "value": "변경" },
  { "op": "test", "path": "/data/mark_ups/0/mark_up_id", "value": 1 },
  { "op": "replace", "path": "/data/mark_ups/0/mark_up_name", "value": "수정" },
  { "op": "add", "path": "/data/mark_ups/-", "value": { "mark_up_id": 2, "mark_up_name": "추가" } }
]
```

- 지원 작업: `add`, `replace`, `remove`, `test`. `copy`/`move`는 지원하지 않으며 400을 반환합니다.
- 경로는 응답 구조 기준 `/data` 또는 그 하위만 허용합니다. `/id`는 변경할 수 없습니다.
- `add`는 객체 속성을 추가/덮어쓰고 배열에는 지정 인덱스에 삽입합니다. `-`는 배열 끝에 추가합니다.
- `replace`/`remove`는 대상이 존재해야 합니다. 배열은 0부터 시작하는 인덱스를 사용하며 ID 기반 검색 경로는 지원하지 않습니다. `test`로 mark_up_id를 확인한 뒤 수정할 수 있습니다.
- 속성명에 `/`가 있으면 `~1`, `~`가 있으면 `~0`으로 경로에 표기합니다.
- `value: null`은 JSON null을 저장합니다. 속성 삭제는 `remove`입니다. 중간 부모는 존재해야 합니다.
- `/data` 전체를 객체로 교체할 수 있지만 삭제하거나 null/배열로 바꿀 수는 없습니다.
- 작업은 순서대로 적용되며 하나라도 실패하면 전체 저장을 취소합니다. 잘못된 경로/인덱스는 400, test 값 불일치는 409, 다른 Content-Type은 415입니다.
- 최대 100개 작업, 요청 본문 최대 1 MiB입니다. 빈 작업 배열은 변경 없이 현재 데이터를 반환합니다.
- DB 트랜잭션과 행 잠금으로 동시 PATCH 요청을 순서대로 처리하여 서로 다른 필드 변경이 유실되지 않게 합니다. 오래된 클라이언트 값 검증이 필요하면 `test`를 포함하세요. PUT은 여전히 전체 교체입니다.
- 엔티티와 DB 컬럼은 동일하여 이 기능에 새 마이그레이션은 필요하지 않습니다.

요청 모델은 `Models/Requests/LayerRequest.cs`의 `CreateLayerRequest`(생성: id/data), `LayerDataRequest`(수정: data)를 사용합니다.

### 수동 API 테스트

[Layer.Api.http](Layer.Api.http)를 Visual Studio 또는 HTTP 요청 실행을 지원하는 편집기에서 열어 순서대로 실행하세요. 테스트 전용 `layerId`가 기존 데이터와 겹치지 않는지 확인하세요.

| 케이스 | 기대 결과 |
|---|---|
| 정상 생성 → 목록·단건 조회 | 201 → 200, JSON 구조 보존 |
| 동일 ID 재생성 | 409 |
| data 전체 교체 → 재조회 | 200, 중첩 객체·빈 배열·추가 필드 보존 |
| 잘못된 ID 길이 / 비16진수 문자 | 400 |
| data 누락 / null / 배열 / 문자열 | 400 |
| 삭제 → 재조회·수정·재삭제 | 204 → 404 |

### 자동 통합 테스트

`scripts/Test-LayerCrud.ps1`은 임의의 24자리 테스트 ID를 만들고 실제 PostgreSQL을 대상으로 검증한 뒤 해당 행을 삭제합니다. 수동 테스트와 별도로 실행할 수 있습니다.

```powershell
dotnet run
# 다른 PowerShell 7 터미널에서 실행
pwsh -File scripts/Test-LayerCrud.ps1
```

테스트는 생성한 행만 정리합니다. DB 직접 조회:

```sql
SELECT id, pg_typeof(data), data FROM democrud_demo."LAYER";
```

연결 문자열은 Git 제외 파일 `appsettings.Local.json`에 있습니다. 기존 `/api/items` 테스트 API도 유지합니다.

참고: [Npgsql JSON 매핑](https://www.npgsql.org/efcore/mapping/json.html).

