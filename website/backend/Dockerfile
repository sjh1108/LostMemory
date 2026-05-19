# syntax=docker/dockerfile:1.7
ARG JDK_IMAGE=eclipse-temurin:17-jdk-jammy
ARG JRE_IMAGE=eclipse-temurin:17-jre-jammy

# =========================
# 1) Build stage
# =========================
FROM ${JDK_IMAGE} AS builder
WORKDIR /workspace

# 의존성 해석에 필요한 파일만 먼저 복사해서 레이어 캐시 활용
COPY gradle gradle
COPY gradlew settings.gradle build.gradle ./
RUN chmod +x gradlew && ./gradlew --version --no-daemon

# 소스 변경 없으면 이 단계 캐시 재사용
RUN ./gradlew dependencies --no-daemon || true

# 실제 소스 복사 후 bootJar
COPY src src
RUN ./gradlew clean bootJar --no-daemon -x test

# layered jar 추출 (Spring Boot 3.x 기본 layered 지원)
RUN java -Djarmode=layertools -jar build/libs/*.jar extract --destination build/layers

# =========================
# 2) Runtime stage
# =========================
FROM ${JRE_IMAGE} AS runtime

ENV TZ=Asia/Seoul \
    JAVA_OPTS="" \
    SPRING_PROFILES_ACTIVE=prod

WORKDIR /app

RUN groupadd --system app \
 && useradd --system --gid app --home /app app \
 && mkdir -p /var/log/app \
 && chown -R app:app /app /var/log/app \
 && apt-get update \
 && apt-get install -y --no-install-recommends curl tzdata \
 && rm -rf /var/lib/apt/lists/*

USER app

# layered jar 레이어들 순서대로 복사 (변경 빈도 낮음 → 높음)
COPY --from=builder --chown=app:app /workspace/build/layers/dependencies/          ./
COPY --from=builder --chown=app:app /workspace/build/layers/spring-boot-loader/    ./
COPY --from=builder --chown=app:app /workspace/build/layers/snapshot-dependencies/ ./
COPY --from=builder --chown=app:app /workspace/build/layers/application/           ./

EXPOSE 8081

HEALTHCHECK --interval=15s --timeout=5s --retries=6 --start-period=40s \
  CMD curl -fsS http://localhost:8081/actuator/health | grep -q '"status":"UP"' || exit 1

ENTRYPOINT ["sh","-c","exec java $JAVA_OPTS org.springframework.boot.loader.launch.JarLauncher"]
