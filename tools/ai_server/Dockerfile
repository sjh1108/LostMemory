# syntax=docker/dockerfile:1.7
ARG JDK_IMAGE=eclipse-temurin:17-jdk-jammy
ARG JRE_IMAGE=eclipse-temurin:17-jre-jammy

FROM ${JDK_IMAGE} AS builder
WORKDIR /workspace

COPY gradle gradle
COPY gradlew settings.gradle build.gradle ./
RUN chmod +x gradlew && ./gradlew --version --no-daemon

COPY src src
RUN ./gradlew clean bootJar --no-daemon -x test
RUN java -Djarmode=layertools -jar build/libs/ai-server.jar extract --destination build/layers

FROM ${JRE_IMAGE} AS runtime

ENV TZ=Asia/Seoul \
    JAVA_OPTS="" \
    SPRING_PROFILES_ACTIVE=prod

WORKDIR /app

RUN groupadd --system app \
 && useradd --system --gid app --home /app app \
 && apt-get update \
 && apt-get install -y --no-install-recommends curl tzdata \
 && rm -rf /var/lib/apt/lists/*

USER app

COPY --from=builder --chown=app:app /workspace/build/layers/dependencies/ ./ 
COPY --from=builder --chown=app:app /workspace/build/layers/spring-boot-loader/ ./ 
COPY --from=builder --chown=app:app /workspace/build/layers/snapshot-dependencies/ ./ 
COPY --from=builder --chown=app:app /workspace/build/layers/application/ ./ 

EXPOSE 8080

HEALTHCHECK --interval=15s --timeout=5s --retries=6 --start-period=40s \
  CMD curl -fsS http://localhost:8080/api/actuator/health | grep -q '"status":"UP"' || exit 1

ENTRYPOINT ["sh","-c","exec java $JAVA_OPTS org.springframework.boot.loader.launch.JarLauncher"]
