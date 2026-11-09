# =========================================
# Stage 1: Build (Maven)
# =========================================
FROM maven:3.9-eclipse-temurin-21 AS build
WORKDIR /build

# 1. نسخ ملفات الـ protos أولاً لأن المشروع يعتمد عليها
COPY protos ./protos

# 2. نسخ مجلد المشروع بالكامل لكي يتمكن Maven من العثور على الكود والمجلدات (بما فيها src و pom.xml)
COPY src/WarehouseServiceJava ./src/WarehouseServiceJava

# 3. الانتقال إلى مجلد الخدمة
WORKDIR /build/src/WarehouseServiceJava

# 4. بناء المشروع وتوليد الحزمة مع استخدام ميزة الكاش لتسريع العملية وتجنب إعادة التحميل البطيء
RUN --mount=type=cache,target=/root/.m2 mvn -B -DskipTests package

# =========================================
# Stage 2: Run (JRE فقط)
# =========================================
FROM eclipse-temurin:21-jre
WORKDIR /app
COPY --from=build /build/src/WarehouseServiceJava/target/warehouse-service-*.jar app.jar
EXPOSE 8080
ENTRYPOINT ["java", "-jar", "app.jar"]