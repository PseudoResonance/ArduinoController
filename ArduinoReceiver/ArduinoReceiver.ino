#include <MSMotorShield.h>
#include <ControllerReceiver.h>

#define BAUD_RATE 115200

#define MAX_SPEED 200

void setup() {
	init(BAUD_RATE);
}

void loop() {
	if (runLoop()) {
		runControls();
	}
}

double speed, rotation, test, maxSpeed, leftSpeed, rightSpeed;

void runControls() {
	if (!isButtonPressed(BUTTON_A)) {
		speed = getRightTrigger() - getLeftTrigger();
		rotation = getLeftX();
		speed = (speed * speed) * (speed > 0 ? 1 : -1);
		rotation = (rotation * rotation) * (rotation > 0 ? 1 : -1);

		test = max(abs(speed), abs(rotation));
		maxSpeed = (test * test) * (test > 0 ? 1 : -1);

		if (speed >= 0) {
			if (rotation >= 0) {
				leftSpeed = maxSpeed;
				rightSpeed = speed - rotation;
			}
			else {
				leftSpeed = speed + rotation;
				rightSpeed = maxSpeed;
			}
		}
		else {
			if (rotation >= 0) {
				leftSpeed = speed + rotation;
				rightSpeed = -maxSpeed;
			}
			else {
				leftSpeed = -maxSpeed;
				rightSpeed = speed - rotation;
			}
		}
		setMotor(getMotor1(), (uint8_t)round(abs(leftSpeed) * MAX_SPEED), leftSpeed < 0);
		setMotor(getMotor2(), (uint8_t)round(abs(rightSpeed) * MAX_SPEED), rightSpeed < 0);
		digitalWrite(LED_BUILTIN, HIGH);
	}
	else {
		setMotor(getMotor1(), 0, false);
		setMotor(getMotor2(), 0, false);
	}
}
