-- Fix: the seeded "renewal" template always rendered the "เบี้ยต่ออายุโดยประมาณ" line, even when
-- the reminder carried no quote (the {{estimatedPremium}} token then held a placeholder string).
-- Switch to a single {{premiumLine}} token that the helper fills with the whole line (label + price)
-- when a quote exists, and leaves empty otherwise — so the line is omitted, not shown blank.
UPDATE notification_template
SET body = N'เรียน {{customerName}}
กรมธรรม์เลขที่ {{policyNo}} จะหมดอายุวันที่ {{expiryDate}} กรุณาติดต่อเจ้าหน้าที่เพื่อต่ออายุความคุ้มครอง{{premiumLine}}'
WHERE template_key = 'renewal';
