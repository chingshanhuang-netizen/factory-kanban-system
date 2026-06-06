-- ============================================================
-- TPS.Nexus.Kanban — Demo 測試資料
-- Version : v1.0
-- Date    : 2026-06-07
-- 說明    : 與 DemoEquipmentService / DemoLayoutService /
--           DemoAlarmService 的 Mock 資料對齊
-- ============================================================
-- 前置條件：已執行 2026-06-07-kanban-mysql-schema.sql
-- 執行前先確認 kanban_factory_maps 有 Floor / Area 欄位
-- ============================================================

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ============================================================
-- 0. 欄位遷移（各欄獨立執行，已存在時 dbinit 記錄 ERR 但繼續）
-- ============================================================
ALTER TABLE kanban_factory_maps      ADD COLUMN Floor     VARCHAR(50)  NULL COMMENT '樓層'       AFTER ThumbnailPath;
ALTER TABLE kanban_factory_maps      ADD COLUMN Area      VARCHAR(100) NULL COMMENT '廠區'       AFTER Floor;
ALTER TABLE kanban_equipment         ADD COLUMN Category  VARCHAR(100) NULL COMMENT '設備類別'   AFTER Name;
ALTER TABLE kanban_equipment         ADD COLUMN MapName   VARCHAR(200) NULL COMMENT '所屬地圖'   AFTER Category;
ALTER TABLE kanban_datasource_configs ADD COLUMN DataType VARCHAR(50)  NULL COMMENT '資料類型'  AFTER SourceType;

-- ============================================================
-- 1. 清除舊測試資料（按 FK 逆序）
-- ============================================================
DELETE FROM kanban_equipment_link_configs;
DELETE FROM kanban_alarm_records;
DELETE FROM kanban_alarm_rules;
DELETE FROM kanban_widget_components;
DELETE FROM kanban_equipment_widgets;
DELETE FROM kanban_datasource_configs;
DELETE FROM kanban_equipment;
DELETE FROM kanban_layout_versions;
DELETE FROM kanban_factory_maps;

-- 重設 AUTO_INCREMENT，讓 ID 與 Mock 資料一致
ALTER TABLE kanban_factory_maps           AUTO_INCREMENT = 1;
ALTER TABLE kanban_layout_versions        AUTO_INCREMENT = 1;
ALTER TABLE kanban_equipment              AUTO_INCREMENT = 1;
ALTER TABLE kanban_datasource_configs     AUTO_INCREMENT = 1;
ALTER TABLE kanban_equipment_widgets      AUTO_INCREMENT = 1;
ALTER TABLE kanban_widget_components      AUTO_INCREMENT = 1;
ALTER TABLE kanban_alarm_rules            AUTO_INCREMENT = 1;
ALTER TABLE kanban_alarm_records          AUTO_INCREMENT = 1;
ALTER TABLE kanban_equipment_link_configs AUTO_INCREMENT = 1;

-- ============================================================
-- 2. kanban_factory_maps  (2 張地圖)
-- ============================================================
INSERT INTO kanban_factory_maps
    (Id, Name, FormatType, FilePath, ThumbnailPath, CreatedAt,
     Floor, Area, Version, CarouselEnabled, CarouselSeconds, CarouselOrder)
VALUES
    (1, '廠區一樓平面圖', 2, '/maps/demo-floor.svg', NULL,
     DATE_SUB(NOW(), INTERVAL 30 DAY),
     '1F', '主廠區', 'v2', 1, 15, 1),
    (2, '廠區二樓平面圖', 2, '/maps/demo-floor.svg', NULL,
     DATE_SUB(NOW(), INTERVAL 15 DAY),
     '2F', '主廠區', 'v1', 1, 15, 2);

-- ============================================================
-- 3. kanban_layout_versions  (3 個版本給地圖1，1 個給地圖2)
-- ============================================================
INSERT INTO kanban_layout_versions
    (Id, FactoryMapId, VersionNo, Status, CreatedBy, PublishedAt, LayoutJson)
VALUES
    (1, 1, 1, 2, '系統管理員', DATE_SUB(NOW(), INTERVAL 14 DAY),
     '{"note":"initial layout"}'),
    (2, 1, 2, 2, '系統管理員', DATE_SUB(NOW(), INTERVAL 7 DAY),
     '{"note":"added robot widget"}'),
    (3, 1, 3, 1, '系統管理員', DATE_SUB(NOW(), INTERVAL 1 DAY),
     '{"note":"added QC station","widgets":[{"equipmentId":1,"x":130,"y":230},{"equipmentId":2,"x":460,"y":230},{"equipmentId":3,"x":790,"y":230}]}'),
    (4, 2, 1, 1, '系統管理員', DATE_SUB(NOW(), INTERVAL 5 DAY),
     '{"note":"2F initial layout"}');
-- Status: 0=Draft 1=Published 2=Archived

-- ============================================================
-- 4. kanban_equipment  (15 台設備)
-- ============================================================
INSERT INTO kanban_equipment
    (Id, Name, Category, MapName, Tag, Description, IconType, IconValue)
VALUES
    -- 廠區一樓
    ( 1, 'CNC加工機',    '加工設備', '廠區一樓平面圖', 'CNC-01', '5軸加工中心',        0, 'icon-cnc'),
    ( 2, '組裝機器人',   '組裝設備', '廠區一樓平面圖', 'ASM-01', '六軸工業機器人',      0, 'icon-robot'),
    ( 3, '品質檢測站',   '檢測設備', '廠區一樓平面圖', 'QC-01',  '3D視覺量測系統',      0, 'icon-qc'),
    ( 4, '雷射切割機',   '加工設備', '廠區一樓平面圖', 'LSR-01', 'CO₂雷射切割',         0, 'icon-laser'),
    ( 5, '沖壓成型機',   '加工設備', '廠區一樓平面圖', 'PRS-01', '100T油壓沖床',        0, 'icon-press'),
    ( 6, '焊接機器人',   '組裝設備', '廠區一樓平面圖', 'WLD-01', 'MIG/MAG自動焊接',     0, 'icon-weld'),
    ( 7, '輸送帶系統',   '物流設備', '廠區一樓平面圖', 'CVY-01', '主線輸送皮帶',        0, 'icon-conveyor'),
    ( 8, 'AGV搬運車',    '物流設備', '廠區一樓平面圖', 'AGV-01', '自動導引搬運車',      0, 'icon-agv'),
    -- 廠區二樓
    ( 9, '噴塗機器人',   '表面處理', '廠區二樓平面圖', 'SPR-01', '六軸自動噴漆臂',      0, 'icon-spray'),
    (10, '烤漆烘烤爐',   '表面處理', '廠區二樓平面圖', 'OVN-01', '紅外線烘烤隧道爐',    0, 'icon-oven'),
    (11, '壓縮空氣站',   '公用設施', '廠區二樓平面圖', 'AIR-01', '螺旋式空壓機組',      0, 'icon-air'),
    (12, '冷卻水塔',     '公用設施', '廠區二樓平面圖', 'CLT-01', '循環冷卻水系統',      0, 'icon-cooling'),
    (13, 'CMM座標量測機','檢測設備', '廠區二樓平面圖', 'CMM-01', '三次元座標量測儀',    0, 'icon-cmm'),
    (14, 'X光探傷儀',    '檢測設備', '廠區二樓平面圖', 'XRY-01', '工業X射線檢測',       0, 'icon-xray'),
    (15, '廢水處理站',   '環保設備', '廠區二樓平面圖', 'WWT-01', '工業廢水過濾系統',    0, 'icon-waste');
-- IconType: 0=CssClass

-- ============================================================
-- 5. kanban_datasource_configs  (3 筆資料來源)
-- ============================================================
INSERT INTO kanban_datasource_configs
    (Id, Name, SourceType, DataType, ConnectionString, FilePath, QueryOrPath, Parameters)
VALUES
    (1, 'CNC 主軸溫度',   1, '溫度', NULL, '/data/cnc-temp.csv',  NULL, NULL),
    (2, '機器人電流感測', 1, '電流', NULL, '/data/robot-amp.csv', NULL, NULL),
    (3, '檢測站產量計數', 2, '計數', NULL, '/data/qc-count.json', NULL, NULL);
-- SourceType: 0=SQL 1=CSV 2=JSON 3=XML

-- ============================================================
-- 6. kanban_equipment_widgets  (地圖1 Published版本 3 個 widget)
-- ============================================================
INSERT INTO kanban_equipment_widgets
    (Id, EquipmentId, LayoutVersionId, PositionX, PositionY, Width, Height)
VALUES
    (1, 1, 3, 130, 230, 100, 120),   -- CNC加工機
    (2, 2, 3, 460, 230, 100, 120),   -- 組裝機器人
    (3, 3, 3, 790, 230, 100, 120);   -- 品質檢測站

-- ============================================================
-- 7. kanban_widget_components  (每個 widget 一個狀態燈元件)
-- ============================================================
INSERT INTO kanban_widget_components
    (Id, EquipmentWidgetId, ComponentType, DataSourceConfigId,
     Label, Unit, RefreshInterval, DisplayOrder, ConfigJson)
VALUES
    (1, 1, 0, 1, '主軸溫度', '℃',  30, 0, NULL),   -- CNC  → StatusIndicator → CSV
    (2, 1, 1, 1, '主軸溫度', '℃',  30, 1, NULL),   -- CNC  → ValueGauge
    (3, 2, 0, 2, '電流',     'A',   30, 0, NULL),   -- Robot → StatusIndicator → CSV
    (4, 3, 0, 3, '產量',     '件',  60, 0, NULL);   -- QC   → StatusIndicator → JSON
-- ComponentType: 0=StatusIndicator 1=ValueGauge 2=TrendChart

-- ============================================================
-- 8. kanban_alarm_rules  (CNC溫度警告規則)
-- ============================================================
INSERT INTO kanban_alarm_rules
    (Id, EquipmentId, DataSourceConfigId, FieldName, `Condition`,
     Threshold, AlarmLevel, Message, IsEnabled)
VALUES
    (1, 1, 1, 'temperature', '>',  80.0, 1,
     'CNC加工機 主軸溫度超過警戒值 {value} ℃', 1),
    (2, 1, 1, 'temperature', '>',  95.0, 2,
     'CNC加工機 主軸溫度嚴重過熱 {value} ℃，請立即停機', 1),
    (3, 2, 2, 'current',     '>',  15.0, 1,
     '組裝機器人 電流異常 {value} A', 1),
    (4, 2, 2, 'current',     '>',  20.0, 2,
     '組裝機器人 過電流保護觸發 {value} A', 1);
-- AlarmLevel: 0=Info 1=Warning 2=Critical

-- ============================================================
-- 9. kanban_alarm_records  (1 筆活躍警報 + 2 筆歷史已解除)
-- ============================================================
INSERT INTO kanban_alarm_records
    (Id, EquipmentId, EquipmentName, AlarmRuleId, Level,
     Message, TriggeredAt, ResolvedAt)
VALUES
    -- 活躍警報（ResolvedAt IS NULL）
    (1, 1, 'CNC加工機', 1, 1,
     'CNC加工機: 主軸溫度 > 80.0 (actual: 87.3 ℃)',
     DATE_SUB(NOW(), INTERVAL 35 MINUTE), NULL),

    -- 已解除歷史紀錄
    (2, 2, '組裝機器人', 3, 1,
     '組裝機器人: 電流 > 15.0 (actual: 16.2 A)',
     DATE_SUB(NOW(), INTERVAL 3 DAY),
     DATE_ADD(DATE_SUB(NOW(), INTERVAL 3 DAY), INTERVAL 45 MINUTE)),
    (3, 1, 'CNC加工機', 1, 1,
     'CNC加工機: 主軸溫度 > 80.0 (actual: 82.1 ℃)',
     DATE_SUB(NOW(), INTERVAL 7 DAY),
     DATE_ADD(DATE_SUB(NOW(), INTERVAL 7 DAY), INTERVAL 2 HOUR));

-- ============================================================
-- 10. kanban_equipment_link_configs  (CNC 詳情抽屜頁籤)
-- ============================================================
INSERT INTO kanban_equipment_link_configs
    (Id, EquipmentId, LinkType, TabLabel, UrlTemplate, DataSourceConfigId, DisplayOrder)
VALUES
    (1, 1, 0, '工單紀錄',  '/workorders?equipment={equipmentId}', NULL, 0),
    (2, 1, 1, '警報歷史',  NULL,                                  NULL, 1),
    (3, 1, 2, '設備文件',  '/docs/cnc-01-manual.pdf',             NULL, 2),
    (4, 1, 3, '即時監控',  'http://scada.local/cnc-01',           NULL, 3),
    (5, 2, 0, '工單紀錄',  '/workorders?equipment={equipmentId}', NULL, 0),
    (6, 2, 1, '警報歷史',  NULL,                                  NULL, 1),
    (7, 3, 0, '工單紀錄',  '/workorders?equipment={equipmentId}', NULL, 0),
    (8, 3, 1, '警報歷史',  NULL,                                  NULL, 1),
    (9, 3, 3, '量測報表',  'http://qms.local/qc-01/report',       NULL, 2);
-- LinkType: 0=WorkOrder 1=AlarmHistory 2=Document 3=CustomUrl

SET FOREIGN_KEY_CHECKS = 1;

-- ============================================================
-- 驗證查詢
-- ============================================================
SELECT '=== kanban_factory_maps ===' AS '';
SELECT Id, Name, Floor, Version, CarouselEnabled, CarouselOrder FROM kanban_factory_maps;

SELECT '=== kanban_layout_versions ===' AS '';
SELECT Id, FactoryMapId, VersionNo,
       CASE Status WHEN 0 THEN 'Draft' WHEN 1 THEN 'Published' WHEN 2 THEN 'Archived' END AS Status,
       CreatedBy FROM kanban_layout_versions ORDER BY FactoryMapId, VersionNo;

SELECT '=== kanban_equipment (by category) ===' AS '';
SELECT Category, COUNT(*) AS cnt FROM kanban_equipment GROUP BY Category ORDER BY Category;

SELECT '=== kanban_equipment_widgets ===' AS '';
SELECT w.Id, e.Name AS Equipment, w.LayoutVersionId, w.PositionX, w.PositionY
FROM kanban_equipment_widgets w JOIN kanban_equipment e ON e.Id = w.EquipmentId;

SELECT '=== kanban_alarm_records (active) ===' AS '';
SELECT Id, EquipmentName, Level, Message, TriggeredAt
FROM kanban_alarm_records WHERE ResolvedAt IS NULL;
