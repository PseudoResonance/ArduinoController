#include <MSMotorShield.h>

#define BAUD_RATE 115200

#define SERIAL_TIMEOUT 50
#define SAFETY_TIMEOUT 250

#define MAX_SPEED 200

union doubleArray {
	byte array[4];
	double num;
} motor1Array;
union doubleArray motor2Array;
union doubleArray motor3Array;
union doubleArray motor4Array;

MS_DCMotor motor1(1);
MS_DCMotor motor2(2);
MS_DCMotor motor3(3);
MS_DCMotor motor4(4);

void setup() {
	Serial.begin(BAUD_RATE);
	pinMode(LED_BUILTIN, OUTPUT);
	digitalWrite(LED_BUILTIN, LOW);
	motor1.run(RELEASE);
	motor2.run(RELEASE);
	motor3.run(RELEASE);
	motor4.run(RELEASE);
}

void setMotor(MS_DCMotor* motor, uint8_t speed, bool reverse) {
	if (speed > 0) {
		if (!reverse)
			motor->run(FORWARD);
		else
			motor->run(BACKWARD);
		motor->setSpeed(speed);
	}
	else {
		motor->run(BRAKE);
		motor->setSpeed(0);
	}
}

void runInput() {
	setMotor(&motor1, (uint8_t)round(abs(motor1Array.num) * MAX_SPEED), motor1Array.num < 0);
	setMotor(&motor2, (uint8_t)round(abs(motor2Array.num) * MAX_SPEED), motor2Array.num < 0);
	setMotor(&motor3, (uint8_t)round(abs(motor3Array.num) * MAX_SPEED), motor3Array.num < 0);
	setMotor(&motor4, (uint8_t)round(abs(motor4Array.num) * MAX_SPEED), motor4Array.num < 0);
	digitalWrite(LED_BUILTIN, HIGH);
}

void halt() {
	motor1.setSpeed(0);
	motor2.setSpeed(0);
	motor3.setSpeed(0);
	motor4.setSpeed(0);
	motor1.run(BRAKE);
	motor2.run(BRAKE);
	motor3.run(BRAKE);
	motor4.run(BRAKE);
	digitalWrite(LED_BUILTIN, LOW);
}

byte last;
uint16_t charsReceived;
bool receiving = false;

unsigned long lastRec, startRec;

void loop() {
	if (Serial.available() > 0) {
		byte in = Serial.read();
		if (receiving) {
			uint8_t i = charsReceived % 4;
			if (charsReceived < 4) {
				motor1Array.array[i] = in;
			}
			else if (charsReceived < 8) {
				motor2Array.array[i] = in;
			}
			else if (charsReceived < 12) {
				motor3Array.array[i] = in;
			}
			else if (charsReceived < 16) {
				motor4Array.array[i] = in;
			}
			else {
				receiving = false;
				runInput();
				lastRec = millis();
			}
			charsReceived++;
		}
		else {
			if (in == 114 && last == 67) {
				charsReceived = 0;
				receiving = true;
				last = 0;
				startRec = millis();
			}
			else {
				if (millis() - lastRec > SAFETY_TIMEOUT) {
					halt();
				}
				last = in;
			}
		}
	}
	else {
		if (receiving && millis() - startRec > SERIAL_TIMEOUT) {
			receiving = false;
			last = 0;
		}
	}
}
