생성 결과 PNG는 Git에 커밋하지 않는다.

- 이유: `*.png`는 Git LFS 대상인데, 일반 Git blob으로 들어가면 checkout/merge 시 LFS pointer 오류가 난다.
- 보존 기준: workflow JSON은 저장소에 남기고, 생성 이미지는 로컬 또는 별도 공유 경로에 보관한다.
