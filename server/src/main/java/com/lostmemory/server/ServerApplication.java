package com.lostmemory.server;

import org.springframework.boot.WebApplicationType;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.boot.context.properties.ConfigurationPropertiesScan;

@SpringBootApplication
@ConfigurationPropertiesScan
public class ServerApplication {

	public static void main(String[] args) {
		// webflux starter 가 의존성에 포함됨 (LlmProxyService 의 WebClient 만 사용).
		// 그러나 실제 서버는 servlet (Tomcat) — Builder 로 명시.
		// application.yaml 의 spring.main.web-application-type 명시는 RelayApplication 의
		// WebApplicationType.NONE 까지 override 하므로 사용하지 않는다 (그 사고 lesson).
		new SpringApplicationBuilder(ServerApplication.class)
				.web(WebApplicationType.SERVLET)
				.run(args);
	}

}
