# 활협전 수정 Mod

[繁體中文](README.md) | [English](README.en.md) | [日本語](README.ja.md) | 한국어

**활협전(活俠傳, Legend of Mortal)** 용 게임 내 치트/디버그 메뉴입니다(BepInEx 6 플러그인).

> [!NOTE]
> 이 페이지에는 설치 방법과 간단한 기능 소개만 있습니다. 각 기능의 자세한 설명은 [English README](README.en.md)를 참고하세요.
> 메뉴는 번체 중국어로만 표시되며 한국어는 지원하지 않습니다.

## 설치

> [!IMPORTANT]
> 이 Mod는 **BepInEx 6**(Unity Mono 버전, `6.0.0-be.697` 권장) 전용입니다. **BepInEx 5에서는 작동하지 않습니다.**
> BepInEx 5에서는 Mod가 전혀 로드되지 않으며 `BepInEx/LogOutput.log`에 `0 plugins to load`가 표시됩니다.
> 구별 방법: `BepInEx/core`에 `BepInEx.Unity.Mono.dll`이 있으면 6, `BepInEx.dll`만 있으면 5입니다.

> [!NOTE]
> 현재 버전은 게임 **1.0.6001.0**(타이틀 화면 왼쪽 아래에 표시)에 맞춰져 있습니다. 이전 버전의 게임에서는 「劇情中隨時存檔」(스토리 중 저장)와 회상 기록 기능이 작동하지 않습니다.

- [Releases](https://github.com/KazeNoYumeX/legend-of-mortal-mod/releases/latest)에서 `legend-of-mortal-mod.zip`을 다운로드합니다. BepInEx 6 be.697이 포함되어 있고 `Type = Camera`도 설정되어 있습니다.
- `legend-of-mortal-mod.zip`을 게임 폴더에 압축 해제하고, 같은 이름의 파일은 덮어씁니다. 이전에 BepInEx 5를 설치했다면 먼저 게임 폴더의 `BepInEx/core`, `winhttp.dll`, `doorstop_config.ini`를 삭제하세요.
- BepInEx를 직접 설치하려면 [BepInEx 6 be.697(Unity Mono x64)](https://builds.bepinex.dev/projects/bepinex_be/697/BepInEx-Unity.Mono-win-x64-6.0.0-be.697%2B5362580.zip)을 다운로드하고, 게임을 한 번 실행한 뒤 종료한 다음 `BepInEx/config/BepInEx.cfg`의 `[Preloader.Entrypoint]` 아래 `Type = Application`을 `Type = Camera`로 바꾸세요(바꾸지 않으면 메뉴가 나타나지 않습니다).
- 骰子大師 등 다른 BepInEx 6 Mod와 함께 사용할 수 있습니다.

### 업데이트

- v1.1.x 이후에서: `BepInEx/plugins/LegendOfMortalMod/LegendOfMortalMod.dll`만 교체합니다.
- v1.0.0에서, 또는 처음 설치: zip 전체를 게임 폴더에 압축 해제하고 덮어씁니다.

## 문제 해결

- `F3`을 눌러도 아무 반응이 없음: BepInEx 6이 설치되어 있는지, `BepInEx/config/BepInEx.cfg`가 `Type = Camera`로 되어 있는지 확인하세요. v1.0.0 zip은 `Application`으로 되어 있었으므로 직접 바꾸거나 최신 버전을 다운로드하세요.
- `BepInEx/LogOutput.log`가 갱신되지 않음: Steam을 완전히 종료한 뒤 다시 열고 Steam에서 게임을 실행하세요.

## 사용법

게임 중 `F3`으로 메뉴를 엽니다. 키는 `BepInEx/config/LegendOfMortalMod.cfg`의 `MenuToggleKey`, 메뉴 크기는 `UiScale`로 바꿀 수 있습니다.

주요 기능:

- 基本: 게임 속도, 행동 횟수 무한, 이번 순 건너뛰기, 소비 횟수 무한, 단조·연단·수련 포인트와 은량 무한, 전투 승패 판정 버튼, 전투 결과와 이벤트 표시, 스토리 플래그 표시, 주사위 결과 지정, 잠긴 선택지와 분기 판정 빠른 해제, 핀볼 총점 변경, 스토리 중 저장, 저장 메모와 칸 수 변경, 연인 변경
- 角色: 능력치, 호감도, 기술 변경과 잠금
- 物品: 서적, 귀중품, 잡화 보유 수, 단조·연단 개발 항목
- 旗標: 스토리 플래그
- 講經堂: 생사부(生死簿)와 한청서(汗青書) 해금

> [!WARNING]
> - **세이브 파일을 수정합니다. 먼저 세이브 파일을 백업하세요.**
> - 플래그나 기술을 직접 바꾸면 스토리가 진행되지 않거나 전투가 깨질 수 있습니다. 변경하기 전에 자주 저장하세요.

스크린샷은 [English README](README.en.md#screenshots)에 있습니다.

> [!NOTE]
> 이 한국어 페이지는 기계 번역으로 작성되었습니다. 잘못된 부분이 있으면 [Issues](https://github.com/KazeNoYumeX/legend-of-mortal-mod/issues)로 알려 주세요.
